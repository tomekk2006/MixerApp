using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Media.Devices;
using Windows.Storage;

namespace MixerApp
{
	public class AppStorage
	{
		public class JsonAudioControl
		{
			public int id { get; set; }
			public int type { get; set; }
			public string mapping { get; set; }
		}
		public class JsonConnection
		{
			public string port { get; set; } // port name
			public int baud { get; set; } // baud rate
			public int noise { get; set; } // noise reduction
		}
		public class JsonObject
		{
			public JsonConnection connection { get; set; }
			public List<JsonAudioControl> controls { get; set; }
		}



		public static async void Save(Connection connection)
		{

			StorageFolder defaultPath = ApplicationData.Current.LocalFolder;
			IAsyncOperation<StorageFile> operation = defaultPath.CreateFileAsync("current.json", CreationCollisionOption.ReplaceExisting);

			JsonConnection jsonConnection = new JsonConnection
			{
				port = connection.PortName,
				baud = connection.BaudRate,
				noise = connection.JumpDistance,
			};
			List<JsonAudioControl> controls = new List<JsonAudioControl>();
			for (int i = 0; i < connection.Sliders.Count; i++)
			{
				foreach (AudioControl audioControl in connection.Sliders[i].AudioControls)
				{
					controls.Add(new JsonAudioControl
					{
						id = i,
						type = ((int)audioControl.SliderType),
						mapping = audioControl.Mapping,
					});
				}
			}
			JsonObject json = new JsonObject()
			{
				connection = jsonConnection,
				controls = controls,
			};
			string JsonString = JsonSerializer.Serialize(json);
			StorageFile file = await operation;
			await FileIO.WriteTextAsync(file, JsonString);
		}

		public static async Task<Connection> Load()
		{

			StorageFolder defaultPath = ApplicationData.Current.LocalFolder;
			StorageFile file = await defaultPath.GetFileAsync("current.json");

			string text = await FileIO.ReadTextAsync(file);
			JsonObject json = JsonSerializer.Deserialize<JsonObject>(text);

			Connection connection = new Connection()
			{
				PortName = json.connection.port,
				BaudRate = json.connection.baud,
				JumpDistance = json.connection.noise,
				DtrEnable = true,
				ReadTimeout = 500
			};
			connection.ControlQueue = json.controls;

			return connection;
		}
		public static async void Delete()
		{
			StorageFolder defaultPath = ApplicationData.Current.LocalFolder;
			StorageFile file = await defaultPath.GetFileAsync("current.json");
			await file.DeleteAsync();
		}
	}
}