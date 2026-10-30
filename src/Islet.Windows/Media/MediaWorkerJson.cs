using System.Text.Json.Serialization;

namespace Islet.Windows.Media;

// .NET 8's built-in generator: fixed protocol models need no runtime reflection graph.
[JsonSerializable(typeof(MediaWorkerRequest))]
[JsonSerializable(typeof(MediaWorkerResponse))]
[JsonSerializable(typeof(MediaWorkerExit))]
[JsonSerializable(typeof(MediaWorkerLifetime))]
public partial class MediaWorkerJson : JsonSerializerContext { }
