using System;
using MTGInstaller.YAML;

namespace MTGInstaller {
	public struct ComponentVersion {
		public ETGModVersion Version;
		public ETGModComponent Component;

		public ComponentVersion(ETGModComponent component, ETGModVersion version) {
			Component = component;
			Version = version;
		}
	}
}
