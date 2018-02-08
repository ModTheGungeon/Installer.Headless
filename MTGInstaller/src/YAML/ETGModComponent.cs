using System;
using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace MTGInstaller.YAML {
	public class ETGModComponent {
		[YamlMember(Alias = "name")]
		public string Name { set; get; }

		[YamlMember(Alias = "author")]
		public string Author { set; get; } = "(Unknown)";

		[YamlMember(Alias = "description")]
		public string Description { set; get; } = "(Missing)";

		[YamlMember(Alias = "versions_url")]
		public string _VersionsURL { set; get; }

		[YamlMember(Alias = "versions")]
		public List<ETGModVersion> _VersionsArray { set; get; }

		public List<ETGModVersion> Versions {
			get {
				if (_VersionsArray != null) return _VersionsArray;
				if (_VersionsURL == null) throw new Exception("Both versions_url and versions aren't set!");
				var str = Downloader.WebClient.DownloadString(_VersionsURL);
				return _VersionsArray = SerializationHelper.Deserializer.Deserialize<List<ETGModVersion>>(str);
			}
		}

		public override string ToString() {
			//if (Beta) return $"[β {Key}] {DisplayName}";
			return $"{Name} w/ {Versions.Count} version(s) (last update: {Versions[0].ReleaseDate ?? "N/A"})";
		}
	}
}
