using System;

namespace MTGInstaller {
	public struct ComponentVersion {
		public ETGModVersion Version;
		public ETGModComponent Component;

		public ComponentVersion(ETGModComponent component, ETGModVersion version) {
			Component = component;
			Version = version;
		}

		public ComponentInfo ComponentInfo { get { return new ComponentInfo(Component.Name, Version.Key); } }
	}
}
