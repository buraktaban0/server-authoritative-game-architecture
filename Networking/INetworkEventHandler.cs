using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tacmind.Networking
{
	public interface INetworkEventHandler
	{

		void OnEventReceived(NetworkTarget sender, NetworkEventType eventType, NetworkStream ns);
		

	}
}
