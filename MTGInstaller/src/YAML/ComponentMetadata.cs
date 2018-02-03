using System;
using YamlDotNet.Serialization;

namespace MTGInstaller.YAML {
	public class ComponentMetadata {
		[YamlMember(Alias = "install_in_subdir")]
		public string[] InstallInSubdir { get; set; }

		// priority over install_in_subdir
		[YamlMember(Alias = "install_in_managed")]
		public string[] InstallInManaged { get; set; }

		[YamlMember(Alias = "name")]
		public string Name { get; set; } = null;
	}
}
