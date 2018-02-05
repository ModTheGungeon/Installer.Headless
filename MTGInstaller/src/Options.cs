using System;
using System.Collections.Generic;
using CommandLine;

namespace MTGInstaller.Options {
	[Verb("download", HelpText = "Download and extract an ETGMod version by key")]
	public class DownloadOptions {
		[Value(0, MetaName = "component", HelpText = "The component", Required = true)]
		public string Component { get; set; }

		[Value(1, MetaName = "version", HelpText = "The version (default is the latest one)", Required = false)]
		public string Version { get; set; }

		[Option('f', "force", HelpText = "Force a redownload (remove the target directory if it already exists)")]
		public bool Force { get; set; }

		[Option('h', "http", HelpText = "Force use of insecure HTTP instead of HTTPS")]
		public bool HTTP { get; set; } 
	}

	[Verb("autodetect", HelpText = "Attempt to autodetect the platform and location of the game")]
	public class AutodetectOptions {
		[Option('p', "platform",
				HelpText = "Force platform")]
		public string Platform { get; set; }
	}

	[Verb("components", HelpText = "List the available components")]
	public class ComponentsOptions {
		[Option('h', "http", HelpText = "Force use of insecure HTTP instead of HTTPS")]
		public bool HTTP { get; set; }

		[Option('c', "components", HelpText = "Add custom components through a YAML file")]
		public IEnumerable<string> CustomComponentFiles { get; set; }
	}

	[Verb("component", HelpText = "Show detailed information about a certain component")]
	public class ComponentOptions {
		[Option('h', "http", HelpText = "Force use of insecure HTTP instead of HTTPS")]
		public bool HTTP { get; set; }

		[Value(0, MetaName = "name", HelpText = "Name of a component (to get specific details)", Required = true)]
		public string Name { get; set; }

		[Option('c', "components", HelpText = "Add custom components through a YAML file")]
		public IEnumerable<string> CustomComponentFiles { get; set; }
	}

	[Verb("install", HelpText = "Install components")]
	public class InstallOptions {
		[Value(0, MetaName = "components", HelpText = "Semicolon separated list of components and versions (e.g. 'ETGMod@0.3;Example@1.0')", Required = true)]
		public string Components { get; set; }

		[Option('e', "executable", HelpText = "Path to the executable (required if autodetection fails; you'll be told to use this option if that happens)")]
		public string Executable { get; set; }

		[Option('h', "http", HelpText = "Force use of insecure HTTP instead of HTTPS")]
		public bool HTTP { get; set; } = false;

		[Option('b', "force-backup", HelpText = "Force a backup to be made (note that if your current files have been tampered, the backup will contain these tampered files!)")]
		public bool ForceBackup { get; set; } = false;

		[Option('f', "force", HelpText = "Skip version checks (unsupported!)")]
		public bool SkipVersionChecks { get; set; } = false;

		[Option('c', "components", HelpText = "Add custom components through a YAML file")]
		public IEnumerable<string> CustomComponentFiles { get; set; }

		[Option('d', "leave-patch-dlls", HelpText = "Don't delete the .mm.dll assemblies after finishing patching")]
		public bool LeavePatchDLLs { get; set; } = false;
	}

	[Verb("uninstall", HelpText = "Revert all components")]
	public class UninstallOptions {
		[Option('e', "executable", HelpText = "Path to the executable (required if autodetection fails; you'll be told to use this option if that happens)")]
		public string Executable { get; set; }
	}
}
