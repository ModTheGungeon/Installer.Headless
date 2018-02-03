using System;
using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace MTGInstaller {
	public class GungeonMetadata {
		public class ExeOrigSubsitution {
			[YamlMember(Alias = "from")]
			public string From { get; set; }

			[YamlMember(Alias = "to")]
			public string To { get; set; }
		}

		[YamlMember(Alias = "exe_orig_subsitutions")]
		public IList<ExeOrigSubsitution> ExeOrigSubsitutions { get; set; }

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
