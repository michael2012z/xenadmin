/* Copyright (c) Citrix Systems, Inc. 
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

using System.Collections.Generic;
using System.Linq;
using XenAdmin.Core;
using XenAPI;
using Console = System.Console;

namespace XenAdmin.Actions
{
	public class AssertCanMigrateAction : AsyncAction
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

		public string disableReason = string.Empty;
		public Failure failure;
		public VM vm;
		public List<SR> targetSrs;
		public Host host;
		public XenAPI.Network targetNetwork;
		public AssertCanMigrateAction(VM vm, List<SR> targetSrs, Host host, XenAPI.Network targetNetwork)
			: base(vm.Connection, "", true)
		{
			this.vm = vm;
			this.targetSrs = targetSrs;
			this.host = host;
			this.targetNetwork = targetNetwork;
		}

		protected override void Run()
		{
			try
			{
				log.InfoFormat("Michael start VM.assert_can_migrate on host {0}", host.opaque_ref);
				PIF managementPif = host.Connection.Cache.PIFs.First(p => p.management);
				XenAPI.Network network = host.Connection.Cache.Resolve(managementPif.network);
				Session session = host.Connection.DuplicateSession();
				Dictionary<string, string> receiveMapping = Host.migrate_receive(session, host.opaque_ref, network.opaque_ref, new Dictionary<string, string>());
				log.InfoFormat("Michael mid VM.assert_can_migrate on host {0}", host.opaque_ref);
				VM.assert_can_migrate(vm.Connection.Session,
									  vm.opaque_ref,
									  receiveMapping,
									  true,
									  GetVdiMap(vm, targetSrs),
									  vm.Connection == host.Connection ? new Dictionary<XenRef<VIF>, XenRef<XenAPI.Network>>() : GetVifMap(vm, targetNetwork),
									  new Dictionary<string, string>());
				log.InfoFormat("Michael finish VM.assert_can_migrate on host {0}", host.opaque_ref);
			}
			catch (Failure failure)
			{
				this.failure = failure;
				log.InfoFormat("Michael fail VM.assert_can_migrate on VM {0}", vm.opaque_ref);
			}
		}

		private Dictionary<XenRef<VDI>, XenRef<SR>> GetVdiMap(VM vm, List<SR> targetSrs)
		{
			var vdiMap = new Dictionary<XenRef<VDI>, XenRef<SR>>();

			foreach (var vbdRef in vm.VBDs)
			{
				VBD vbd = vm.Connection.Resolve(vbdRef);
				if (vbd != null)
				{
					VDI vdi = vm.Connection.Resolve(vbd.VDI);
					if (vdi != null)
					{
						SR sr = vm.Connection.Resolve(vdi.SR);
						if (sr != null && sr.GetSRType(true) != SR.SRTypes.iso)
						{
							// CA-220218: select a storage other than the VDI's current storage to ensure that
							// both source and target SRs will be checked to see if they support migration
							// (when sourceSR == targetSR, the server side skips the check)
							var targetSr = targetSrs.FirstOrDefault(s => s.opaque_ref != sr.opaque_ref);
							if (targetSr != null)
								vdiMap.Add(new XenRef<VDI>(vdi.opaque_ref), new XenRef<SR>(targetSr));
						}
					}
				}
			}

			return vdiMap;
		}

		private Dictionary<XenRef<VIF>, XenRef<XenAPI.Network>> GetVifMap(VM vm, XenAPI.Network targetNetwork)
		{
			var vifMap = new Dictionary<XenRef<VIF>, XenRef<XenAPI.Network>>();

			if (targetNetwork != null)
			{
				List<VIF> vifs = vm.Connection.ResolveAll(vm.VIFs);

				foreach (var vif in vifs)
				{
					vifMap.Add(new XenRef<VIF>(vif.opaque_ref), new XenRef<XenAPI.Network>(targetNetwork));
				}
			}
			return vifMap;
		}

	}
}
