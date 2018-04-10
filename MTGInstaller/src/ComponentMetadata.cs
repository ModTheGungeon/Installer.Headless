using System;
using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace MTGInstaller {
	public class ComponentMetadata {
		[YamlMember(Alias = "install_in_subdir")]
		public string[] InstallInSubdir { get; set; }

		// priority over install_in_subdir
		[YamlMember(Alias = "install_in_managed")]
		public string[] InstallInManaged { get; set; }

		[YamlMember(Alias = "name")]
		public string Name { get; set; } = null;

		[YamlMember(Alias = "ordered_targets")]
		public List<string> OrderedTargets { get; set; } = null;

		// target => {mmdll => dll}
		[YamlMember(Alias = "relink_map")]
		public Dictionary<string, Dictionary<string, string>> RelinkMap { get; set; } = null;
	}
}
