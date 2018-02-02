using System;
using YamlDotNet.Serialization;

namespace MTGInstaller.YAML {
	public class ComponentMetadata {
		[YamlMember(Alias = "install_all_in_subdir")]
		public bool InstallAllInSubdir { get; set; } = false;

		[YamlMember(Alias = "install_in_subdir")]
		public string[] InstallInSubdir { get; set; }
	}
}
