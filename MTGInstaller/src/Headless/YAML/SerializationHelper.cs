using System;
using YamlDotNet.Serialization;

namespace MTGInstaller.YAML {
	public static class SerializationHelper {
		public static Serializer Serializer = new SerializerBuilder().Build();
		public static Deserializer Deserializer = new DeserializerBuilder().Build();
	}
}
