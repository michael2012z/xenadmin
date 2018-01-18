﻿/* Copyright (c) Citrix Systems, Inc. 
 * All rights reserved. 
 * 
 * Redistribution and use in source and binary forms, 
 * with or without modification, are permitted provided 
 * that the following conditions are met: 
 * 
 * *   Redistributions of source code must retain the above 
 *     copyright notice, this list of conditions and the 
 *     following disclaimer. 
 * *   Redistributions in binary form must reproduce the above 
 *     copyright notice, this list of conditions and the 
 *     following disclaimer in the documentation and/or other 
 *     materials provided with the distribution. 
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND 
 * CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, 
 * INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF 
 * MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE 
 * DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR 
 * CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, 
 * SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, 
 * BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR 
 * SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS 
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, 
 * WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING 
 * NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE 
 * OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF 
 * SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using XenAdmin.Commands;
using XenAdmin.Controls;
using XenAdmin.Core;
using XenAdmin.Mappings;
using XenAdmin.Network;
using XenAPI;

namespace XenAdmin.Wizards.GenericPages
{
	/// <summary>
	/// Class representing the page of the ImportAppliance wizard where the user specifies
	/// the targets where the VMs of the appliance will be imported
	/// </summary>
	internal abstract partial class SelectMultipleVMDestinationPage : XenTabPage
	{
		private Dictionary<string, VmMapping> m_vmMappings;
		private IXenObject m_selectedObject;
		private bool updatingDestinationCombobox;
        private bool m_buttonNextEnabled;
        protected List<IXenConnection> ignoredConnections = new List<IXenConnection>();
        private readonly CollectionChangeEventHandler Host_CollectionChangedWithInvoke;

        /// <summary>
        /// Combobox item that can executes a command but also be an IEnableableComboBoxItem
        /// </summary>
        private class AddHostExecutingComboBoxItem : IEnableableComboBoxItem
        {
            public override string ToString()
            {
                return Messages.ADD_POOL_OR_SERVER;
            }

            public void ExecuteCommand(Control parent)
            {
                new AddHostCommand(Program.MainWindow, parent).Execute();
            }

            public bool Enabled
            {
                get { return true; }
            }
        }

	    protected SelectMultipleVMDestinationPage()
		{
			InitializeComponent();
            InitializeText();
            Host_CollectionChangedWithInvoke = Program.ProgramInvokeHandler(CollectionChanged);
			ConnectionsManager.XenConnections.CollectionChanged += CollectionChanged;
            ShowWarning(null);
		}

        public override void PageCancelled()
        {
			test();
            Program.Invoke(Program.MainWindow, ClearComboBox);
            Program.Invoke(Program.MainWindow, ClearDataGridView);
            ChosenItem = null;
        }

        public override void PageLeave(PageLoadedDirection direction, ref bool cancel)
        {
            SetDefaultTarget(ChosenItem);
            Program.Invoke(Program.MainWindow, ClearComboBox);
            base.PageLeave(direction, ref cancel);
        }

	    protected void InitializeText()
	    {
	        m_labelIntro.Text = InstructionText;
	        label1.Text = TargetServerText;
	        label2.Text = TargetServerSelectionIntroText;
            m_colVmName.HeaderText = VmColumnHeaderText;
	        m_colTarget.HeaderText = TargetColumnHeaderText;
	    }

	    private IXenObject _chosenItem;
	    public IXenObject ChosenItem
	    {
	        get { return _chosenItem; }
            protected set
            {
                _chosenItem = value;
                OnChosenItemChanged();
            }
	    }

	    /// <summary>
        /// Text containing instructions for use - at the top of the page
        /// </summary>
	    protected abstract string InstructionText { get; }

        /// <summary>
        /// Text demarking what the label for the target server drop down should be
        /// </summary>
	    protected abstract string TargetServerText { get; }

        protected virtual string VmColumnHeaderText 
        {
            get
            {
                return m_colVmName.HeaderText;
            }
        }

	    protected virtual string TargetColumnHeaderText
	    {
	        get
	        {
	            return m_colTarget.HeaderText;
	        }
	    }
           

        /// <summary>
        /// Text above the table containing a list of VMs and concomitant home server
        /// </summary>
        protected abstract string TargetServerSelectionIntroText { get; }

        protected virtual void OnChosenItemChanged()
        {}

	    protected void ShowWarning(string warningText)
        {
            if (string.IsNullOrEmpty(warningText))
                tableLayoutPanelWarning.Visible = false;
            else
            {
                labelWarning.Text = warningText;
                tableLayoutPanelWarning.Visible = true;
            }
        }

		#region Base class (XenTabPage) overrides

        public override void PageLoaded(PageLoadedDirection direction)
		{
            base.PageLoaded(direction);
            ChosenItem = null;
            restoreGridHomeServerSelection = (direction == PageLoadedDirection.Back);
		}

        public override void SelectDefaultControl()
        {
            m_comboBoxConnection.Select();
        }

        public override bool EnableNext()
        {
            return m_buttonNextEnabled;
        }

		#endregion

		#region Accessors

		public Dictionary<string, VmMapping> VmMappings
		{
			get
			{
				foreach (DataGridViewRow row in m_dataGridView.Rows)
				{
					string sysId = (string)row.Cells[0].Tag;

					if (m_vmMappings.ContainsKey(sysId))
					{
						var mapping = m_vmMappings[sysId];
                        DataGridViewEnableableComboBoxCell cbCell = row.Cells[m_colTarget.Index] as DataGridViewEnableableComboBoxCell;
                        System.Diagnostics.Debug.Assert(cbCell != null, "ComboBox cell was not found");
					    
                        IEnableableXenObjectComboBoxItem selectedItem = cbCell.Value as IEnableableXenObjectComboBoxItem;
						System.Diagnostics.Debug.Assert(selectedItem != null, "Vm has no target mapped");
						var type = selectedItem.Item.GetType();

						if (type == typeof(Pool))
                            mapping.XenRef = new XenRef<Pool>(selectedItem.Item.opaque_ref);
						else if (type == typeof(Host))
                            mapping.XenRef = new XenRef<Host>(selectedItem.Item.opaque_ref);

                        mapping.TargetName = selectedItem.Item.Name();
					}
				}

				return m_vmMappings;
			}
			set { m_vmMappings = value; }
		}

		#endregion

	    public void SetDefaultTarget(IXenObject xenObject)
		{
			m_selectedObject = xenObject;
		}

        protected abstract DelayLoadingOptionComboBoxItem CreateDelayLoadingOptionComboBoxItem(IXenObject xenItem);

		#region Private methods
        
        protected void SetButtonNextEnabled(bool enabled)
        {
            m_buttonNextEnabled = enabled;
            OnPageUpdated();
        }

        private void ClearComboBox()
        {
            Program.AssertOnEventThread();

            foreach (var item in m_comboBoxConnection.Items)
            {
                DelayLoadingOptionComboBoxItem tempItem = item as DelayLoadingOptionComboBoxItem;
                if (tempItem != null)
                    tempItem.ReasonUpdated -= DelayLoadedComboBoxItem_ReasonChanged;
            }
            m_comboBoxConnection.Items.Clear();
        }

		private void test()
		{
			foreach (DataGridViewRow row in m_dataGridView.Rows)
			{
					DataGridViewEnableableComboBoxCell cbCell = row.Cells[m_colTarget.Index] as DataGridViewEnableableComboBoxCell;
					if (cbCell == null)
						return;

					List<IEnableableXenObjectComboBoxItem> list =
						cbCell.Items.OfType<IEnableableXenObjectComboBoxItem>().ToList();
					foreach (IEnableableXenObjectComboBoxItem cbi in list)
					{
						DelayLoadingOptionComboBoxItem it = cbi as DelayLoadingOptionComboBoxItem;
						if (it != null)
							it.cancelFilters();
					}
			}
		}

        private void ClearDataGridView()
        {
            //Clear up comboboxes
            foreach (DataGridViewRow row in m_dataGridView.Rows)
            {
                row.Cells[m_colTarget.Index].Dispose();
            }

            m_dataGridView.Rows.Clear();
            m_dataGridView.Refresh();
        }

		protected void PopulateComboBox()
		{
			Program.AssertOnEventThread();

            Program.Invoke(Program.MainWindow, ClearDataGridView);

			updatingDestinationCombobox = true;
			Program.Invoke(Program.MainWindow, ClearComboBox);

			foreach (var xenConnection in ConnectionsManager.XenConnectionsCopy.Where(con => con.IsConnected).Except(ignoredConnections))
			{
			    DelayLoadingOptionComboBoxItem item = null;

				Pool pool = Helpers.GetPool(xenConnection);

				if (pool == null)
				{
					Host host = Helpers.GetMaster(xenConnection);

					if (host != null)
					{
                        item = CreateDelayLoadingOptionComboBoxItem(host);
                        m_comboBoxConnection.Items.Add(item);
                        item.ReasonUpdated += DelayLoadedComboBoxItem_ReasonChanged;
                        item.Load();
					    host.PropertyChanged -= PropertyChanged;
					    host.PropertyChanged += PropertyChanged;
					}
				}
				else
				{
                    item = CreateDelayLoadingOptionComboBoxItem(pool);
                    m_comboBoxConnection.Items.Add(item);
                    item.ReasonUpdated += DelayLoadedComboBoxItem_ReasonChanged;
                    item.Load();
			        pool.PropertyChanged -= PropertyChanged;
			        pool.PropertyChanged += PropertyChanged;
				}

				if (item != null && m_selectedObject != null && item.Item.Connection == m_selectedObject.Connection)
				    item.PreferAsSelectedItem = true;

				xenConnection.ConnectionStateChanged -= xenConnection_ConnectionStateChanged;
				xenConnection.ConnectionStateChanged += xenConnection_ConnectionStateChanged;
				xenConnection.CachePopulated -= xenConnection_CachePopulated;
				xenConnection.CachePopulated += xenConnection_CachePopulated;
				xenConnection.Cache.RegisterCollectionChanged<Host>(Host_CollectionChangedWithInvoke);
			}

			m_comboBoxConnection.Items.Add(new AddHostExecutingComboBoxItem());
			updatingDestinationCombobox = false;
		}

        protected bool DestinationHasBeenSelected()
        {
            return m_comboBoxConnection.SelectedItem != null;
        }

        private bool MatchingWithXenRefObject(IEnableableXenObjectComboBoxItem item, object xenRef)
        {
            XenRef<Host> hostRef = xenRef as XenRef<Host>;
            if (hostRef != null)
                return hostRef.opaque_ref == item.Item.opaque_ref;

            XenRef<Pool> poolRef = xenRef as XenRef<Pool>;
            if (poolRef != null)
                return poolRef.opaque_ref == item.Item.opaque_ref;

            return false;
        }

        private bool restoreGridHomeServerSelection;

        private void RestoreGridHomeServerSelectionFromMapping()
        {
            foreach (DataGridViewRow row in m_dataGridView.Rows)
            {
                string sysId = (string)row.Cells[0].Tag;
                if (m_vmMappings.ContainsKey(sysId))
                {
                    var mapping = m_vmMappings[sysId];
                    DataGridViewEnableableComboBoxCell cbCell = row.Cells[m_colTarget.Index] as DataGridViewEnableableComboBoxCell;
                    if (cbCell == null)
                        return;

                    List<IEnableableXenObjectComboBoxItem> list =
                        cbCell.Items.OfType<IEnableableXenObjectComboBoxItem>().ToList();
                    IEnableableXenObjectComboBoxItem item =
                        list.FirstOrDefault(cbi => MatchingWithXenRefObject(cbi, mapping.XenRef));
                    if (item != null)
                        cbCell.Value = item;
                }
            }
        }

	    private bool updatingHomeServerList;

        private void PopulateDataGridView(List<ReasoningFilter> homeserverFilters)
        {
            Program.AssertOnEventThread();

            updatingHomeServerList = true;
            try
            {
                Connection = null;

                var target = m_comboBoxConnection.SelectedItem as DelayLoadingOptionComboBoxItem;

                if (target != null)
                    Connection = target.Item.Connection;

                ClearDataGridView();

                SetButtonNextEnabled(true);
                var hasPoolSharedStorage = HasPoolSharedStorage();

                foreach (var kvp in m_vmMappings)
                {
                    var tb = new DataGridViewTextBoxCell {Value = kvp.Value.VmNameLabel, Tag = kvp.Key};
                    var cb = new DataGridViewEnableableComboBoxCell{FlatStyle = FlatStyle.Flat};

                    if (Connection != null)
                    {
                        if (hasPoolSharedStorage)
                        {
                            foreach (var pool in Connection.Cache.Pools)
                            {
                                var item = new NoTargetServerPoolItem(pool);
                                cb.Items.Add(item);

                                if ((m_selectedObject != null && m_selectedObject.opaque_ref == pool.opaque_ref) ||
                                    (target != null && target.Item.opaque_ref == pool.opaque_ref))
                                    cb.Value = item;

                                break; //there exists one pool per connection
                            }
                        }

                        foreach (var host in Connection.Cache.Hosts)
                        {
                            var item = new DelayLoadingOptionComboBoxItem(host, homeserverFilters);
							//item.LoadAndWait();
	                        item.ReasonUpdated += DelayLoadedComboBoxItemX_ReasonChanged;
							item.Load();
                            cb.Items.Add(item);

                            if ((m_selectedObject != null && m_selectedObject.opaque_ref == host.opaque_ref) ||
                                (target != null && target.Item.opaque_ref == host.opaque_ref))
                                cb.Value = item;
                        }
                    }

                    SetComboBoxPreSelection(cb);

                    var row = new DataGridViewRow();
                    row.Cells.AddRange(tb, cb);
                    m_dataGridView.Rows.Add(row);
                }

                HelpersGUI.ResizeLastGridViewColumn(m_colTarget); //set properly the width of the last column

                if (restoreGridHomeServerSelection)
                {
                    RestoreGridHomeServerSelectionFromMapping();
                    restoreGridHomeServerSelection = false;
                }
            }
            finally
            {
                updatingHomeServerList = false;
            }
		}

	    private void SetComboBoxPreSelection(DataGridViewEnableableComboBoxCell cb)
	    {
	        if (cb.Value == null)
	        {
	            if (cb.Items.Count > 0)
	            {
	                if (cb.Items.OfType<IEnableableComboBoxItem>().All(i => !i.Enabled))
	                {
	                    cb.Value = null;
	                    SetButtonNextEnabled(false);
	                }
	                else
	                    cb.Value = cb.Items.OfType<IEnableableComboBoxItem>().First(i => i.Enabled);
	            }  
	            else
	                SetButtonNextEnabled(false); //do not allow to leave the page if a vm has no target
	        }
	    }

	    private bool HasPoolSharedStorage()
		{
			if (Connection == null)
				return false;

			foreach (var pbd in Connection.Cache.PBDs.Where(thePbd => thePbd.SR != null))
			{
				var sr = Connection.Resolve(pbd.SR);
				
				if (sr != null && sr.SupportsVdiCreate() && sr.shared)
						return true;
			}
			return false;
		}

		#endregion

		#region Event Handlers

        private void DelayLoadedComboBoxItem_ReasonChanged(object sender, EventArgs e)
        {
            DelayLoadingOptionComboBoxItem item = sender as DelayLoadingOptionComboBoxItem;

            if (item == null)
                throw new NullReferenceException("Trying to update delay loaded reason but failed to extract reason");

            int index = m_comboBoxConnection.Items.IndexOf(item);
            if (index > -1)
            {
                Program.Invoke( Program.MainWindow, delegate()
                                                        {
                                                            int selectedIndex = m_comboBoxConnection.SelectedIndex;


                                                            if (index > m_comboBoxConnection.Items.Count)
                                                                return;

                                                            if(updatingDestinationCombobox || updatingHomeServerList)
                                                                return;

                                                            DelayLoadingOptionComboBoxItem tempItem = 
                                                                m_comboBoxConnection.Items[index] as DelayLoadingOptionComboBoxItem;
                                                            if(tempItem == null)
                                                                throw new NullReferenceException("Trying to update delay loaded reason but failed to extract reason");
                                                            tempItem.CopyFrom(item);
                                                            m_comboBoxConnection.BeginUpdate();
                                                            m_comboBoxConnection.Items.RemoveAt(index);

                                                            if (updatingDestinationCombobox || updatingHomeServerList)
                                                            {
                                                                m_comboBoxConnection.EndUpdate();
                                                                return;
                                                            }
                                                                
                                                            m_comboBoxConnection.Items.Insert(index, tempItem);
                                                            m_comboBoxConnection.SelectedIndex = selectedIndex;
                                                            m_comboBoxConnection.EndUpdate();

                                                            if (tempItem.PreferAsSelectedItem)
                                                                m_comboBoxConnection.SelectedItem = tempItem;

                                                            item.ReasonUpdated -= DelayLoadedComboBoxItem_ReasonChanged;
                                                        });
            }
        }

		private void DelayLoadedComboBoxItemX_ReasonChanged(object sender, EventArgs e)
		{
			if (m_dataGridView.EditingControl != null && m_dataGridView.EditingControl is ComboBox)
			{
				ComboBox editingControl = m_dataGridView.EditingControl as ComboBox;
				DelayLoadingOptionComboBoxItem item = sender as DelayLoadingOptionComboBoxItem;
				if (item == null)
					throw new NullReferenceException("Trying to update delay loaded reason but failed to extract reason");

				int index = editingControl.Items.IndexOf(item);
				if (index > -1)
				{
					Program.Invoke(Program.MainWindow, delegate ()
					{
						int selectedIndex = editingControl.SelectedIndex;


						if (index > editingControl.Items.Count)
							return;

						if (updatingDestinationCombobox || updatingHomeServerList)
							return;

						DelayLoadingOptionComboBoxItem tempItem =
							editingControl.Items[index] as DelayLoadingOptionComboBoxItem;
						if (tempItem == null)
							throw new NullReferenceException("Trying to update delay loaded reason but failed to extract reason");
						tempItem.CopyFrom(item);
						editingControl.BeginUpdate();
						editingControl.Items.RemoveAt(index);

						if (updatingDestinationCombobox || updatingHomeServerList)
						{
							editingControl.EndUpdate();
							return;
						}

						editingControl.Items.Insert(index, tempItem);
						editingControl.SelectedIndex = selectedIndex;
						editingControl.EndUpdate();

						if (tempItem.PreferAsSelectedItem)
							editingControl.SelectedItem = tempItem;

						item.ReasonUpdated -= DelayLoadedComboBoxItemX_ReasonChanged;
					});
				}
			}
		}

		private void PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == "name_label" || e.PropertyName == "metrics" || e.PropertyName == "enabled" || e.PropertyName == "live" || e.PropertyName == "patches")
				Program.Invoke(this, PopulateComboBox);
            
		}

		private void CollectionChanged(object sender, CollectionChangeEventArgs e)
		{
			Program.BeginInvoke(this, PopulateComboBox);
		}

		private void xenConnection_CachePopulated(object sender, EventArgs e)
        {
			Program.Invoke(this, PopulateComboBox);
        }

		private void xenConnection_ConnectionStateChanged(object sender, EventArgs e)
		{
			Program.Invoke(this, PopulateComboBox);
		}

		#endregion

		#region Control event handlers
		private void m_comboBoxConnection_SelectedIndexChanged(object sender, EventArgs e)
		{
            if (updatingHomeServerList)
                return;

            //If the item is delay loading and them item is disabled, null the selection made 
            //and clear the table containing server data
            IEnableableXenObjectComboBoxItem item = m_comboBoxConnection.SelectedItem as IEnableableXenObjectComboBoxItem;
            if(item != null && !item.Enabled)
            {
                m_comboBoxConnection.SelectedIndex = -1;
                m_dataGridView.Rows.Clear();
                ChosenItem = null;
                return;
            }

		    AddHostExecutingComboBoxItem exeItem = m_comboBoxConnection.SelectedItem as AddHostExecutingComboBoxItem;
            if (exeItem != null && !updatingDestinationCombobox)
                exeItem.ExecuteCommand(this);
				
			else if(!updatingDestinationCombobox)
			{
			    try
			    {
			        Cursor.Current = Cursors.WaitCursor;
			        ChosenItem = item == null ? null : item.Item;
			        Program.Invoke(Program.MainWindow, ()=> PopulateDataGridView(CreateTargetServerFilterList(item)));
			    }
			    finally
			    {
                    Cursor.Current = Cursors.Default;
			    }
			}
                
			IsDirty = true;
		}

        /// <summary>
        /// Create a set of filters for the homeserver combo box selection
        /// </summary>
        /// <param name="item">selected item from the host combobox</param>
        /// <returns></returns>
        protected virtual List<ReasoningFilter> CreateTargetServerFilterList(IEnableableXenObjectComboBoxItem item)
        {
            return new List<ReasoningFilter>();
        }

        private void m_dataGridView_CellClick(object sender, DataGridViewCellEventArgs e)
		{
			if (e.ColumnIndex != m_colTarget.Index || e.RowIndex < 0 || e.RowIndex >= m_dataGridView.RowCount)
				return;

			m_dataGridView.BeginEdit(false);

			if (m_dataGridView.EditingControl != null && m_dataGridView.EditingControl is ComboBox)
				(m_dataGridView.EditingControl as ComboBox).DroppedDown = true;
		}

		private void m_dataGridView_CurrentCellDirtyStateChanged(object sender, EventArgs e)
		{
			m_dataGridView.CommitEdit(DataGridViewDataErrorContexts.Commit);
			IsDirty = true;
		}

		#endregion
        
        private void UnregisterHandlers()
        {
            ConnectionsManager.XenConnections.CollectionChanged -= CollectionChanged;
            foreach (var xenConnection in ConnectionsManager.XenConnectionsCopy)
            {
                var pool = Helpers.GetPool(xenConnection);
                if (pool == null)
                {
                    var host = Helpers.GetMaster(xenConnection);
                    if (host != null)
                        host.PropertyChanged -= PropertyChanged;
                }
                else
                {
                    pool.PropertyChanged -= PropertyChanged;
                }

                xenConnection.ConnectionStateChanged -= xenConnection_ConnectionStateChanged;
                xenConnection.CachePopulated -= xenConnection_CachePopulated;
                xenConnection.Cache.DeregisterCollectionChanged<Host>(Host_CollectionChangedWithInvoke);
            }
        } 
	}
}
