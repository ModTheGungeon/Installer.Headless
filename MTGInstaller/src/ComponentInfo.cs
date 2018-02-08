using System;
using YamlDotNet.Serialization;

namespace MTGInstaller {
	public struct ComponentInfo {
		public ComponentInfo(string name, string version) {
			Name = name;
			Version = version;
		}

		[YamlMember(Alias = "name")]
		public string Name { get; set; }

		[YamlMember(Alias = "version")]
		public string Version { get; set; }
	}
}
