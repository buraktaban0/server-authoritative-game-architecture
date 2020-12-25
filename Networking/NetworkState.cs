using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tacmind.ResourceManagement;

namespace Tacmind.Networking
{
	public abstract class NetworkState : PoolObject, INetworkObject
	{

		public abstract void ReadFrom(NetworkStream ns);

		public abstract void WriteTo(NetworkStream ns);

	}
}
