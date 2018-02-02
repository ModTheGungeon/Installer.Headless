using System;
using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace MTGInstaller {
	public class GungeonMetadata {
		[YamlMember(Alias = "latest_version")]
		public string LatestVersion { get; set; }

		[YamlMember(Alias = "executables")]
		public IList<string> Executables { get; set; }

		[YamlMember(Alias = "managed_files")]
		public IList<string> ManagedFiles { get; set; }

		[YamlMember(Alias = "viable_patch_targets")]
		public IList<string> ViablePatchTargets { get; set; }
	}
}
