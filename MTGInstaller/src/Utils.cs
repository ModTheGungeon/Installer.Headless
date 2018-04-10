using System;
using System.IO;

namespace MTGInstaller {
	public class Utils {
		public static void CopyRecursive(string source, string destination) {
			// https://docs.microsoft.com/en-us/dotnet/standard/io/how-to-copy-directories
			var attr = File.GetAttributes(source);
			if (attr.HasFlag(FileAttributes.Directory)) {
				var dir = new DirectoryInfo(source);

				if (!dir.Exists) {
					throw new DirectoryNotFoundException($"Source directory does not exist or could not be found: {source}");
				}

				var dirs = dir.GetDirectories();
				if (!Directory.Exists(destination)) Directory.CreateDirectory(destination);

				var files = dir.GetFiles();
				foreach (var file in files) {
					var path = Path.Combine(destination, file.Name);
					file.CopyTo(path, overwrite: true);
				}

				foreach (var subdir in dirs) {
					var path = Path.Combine(destination, subdir.Name);
					CopyRecursive(subdir.FullName, path);
				}
			} else {
				File.Copy(source, destination, overwrite: true);
			}
		}
	}
}
