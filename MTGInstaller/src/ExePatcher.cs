using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Diagnostics;
using System.Threading;
using System.Linq;
using System.Text;

namespace MTGInstaller {
	// I should feel bad for being too lazy to implement this on my own.
	// Source: http://stackoverflow.com/a/28607981
	// I should feel bad for being too lazy to reimplement this on my own
	// Copied from old installer ~ zatherz
	public static class ExePatcher {
		public static IEnumerable<byte> GetByteStream(BinaryReader reader) {
			const int bufferSize = 90 ^ 2;
			byte[] buffer;
			do {
				buffer = reader.ReadBytes(bufferSize);
				foreach (var d in buffer) { yield return d; }
			} while (buffer.Length != 0);
		}

		public static void Patch(BinaryReader reader, BinaryWriter writer, IEnumerable<GungeonMetadata.ExeOrigSubsitution> substitutions) {
			foreach (byte d in Patch(GetByteStream(reader), substitutions)) { writer.Write(d); }
		}

		public static IEnumerable<byte> Patch(IEnumerable<byte> source, IEnumerable<GungeonMetadata.ExeOrigSubsitution> substitutions) {
			foreach (var s in substitutions) {
				source = Patch(source, Encoding.UTF8.GetBytes(s.From), Encoding.UTF8.GetBytes(s.To));
			}
			return source;
		}

		public static IEnumerable<byte> Patch(IEnumerable<byte> input, IEnumerable<byte> from, IEnumerable<byte> to) {
			var fromEnumerator = from.GetEnumerator();
			fromEnumerator.MoveNext();
			int match = 0;
			foreach (var data in input) {
				if (data == fromEnumerator.Current) {
					match++;
					if (fromEnumerator.MoveNext()) { continue; }
					foreach (byte d in to) { yield return d; }
					match = 0;
					fromEnumerator.Reset();
					fromEnumerator.MoveNext();
					continue;
				}
				if (0 != match) {
					foreach (byte d in from.Take(match)) { yield return d; }
					match = 0;
					fromEnumerator.Reset();
					fromEnumerator.MoveNext();
				}
				yield return data;
			}
			if (0 != match) {
				foreach (byte d in from.Take(match)) { yield return d; }
			}
		}

	}
}
