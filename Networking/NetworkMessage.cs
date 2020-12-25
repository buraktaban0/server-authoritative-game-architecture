using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tacmind.Networking
{

	public class NetworkMessage
	{

		public static readonly string SEPARATOR = "_%%_", EQUALS = "_==_";


		public string header;
		public Dictionary<string, string> data = new Dictionary<string, string>();

		public string this[string key]
		{
			get
			{
				return data[key];
			}
		}


		public bool ContainsData(string key)
		{
			return data.ContainsKey(key);
		}

		public string GetData(string key)
		{
			return data[key];
		}


		public override string ToString()
		{
			StringBuilder sb = new StringBuilder();
			sb.Append(header);
			foreach (var keyval in data)
			{
				sb.Append(SEPARATOR).Append(keyval.Key).Append(EQUALS).Append(keyval.Value);

			}

			var line = sb.ToString();
			line = Convert.ToBase64String(Encoding.UTF8.GetBytes(line), Base64FormattingOptions.None);
			return line;
		}

		public static NetworkMessage Parse(string line)
		{
			line = Encoding.UTF8.GetString(Convert.FromBase64String(line));

			var parts = line.Split(new string[] { SEPARATOR }, StringSplitOptions.None);

			NetworkMessage msg = new NetworkMessage();
			msg.header = parts[0];

			for (int i = 1; i < parts.Length; i++)
			{
				var keyval = parts[i].Split(new string[] { EQUALS }, StringSplitOptions.None);
				msg.data.Add(keyval[0], keyval[1]);
			}

			return msg;
		}

		public static NetworkMessage Create(string header, params string[] keyval)
		{
			NetworkMessage msg = new NetworkMessage();
			msg.header = header;
			for (int i = 0; i < keyval.Length / 2; i++)
			{
				msg.data[keyval[i * 2]] = keyval[i * 2 + 1];
			}

			return msg;
		}

	}
}
