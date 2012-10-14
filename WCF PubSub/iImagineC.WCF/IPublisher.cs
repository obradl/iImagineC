using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace iImagineC.WCF
{
    public interface IPublisher
    {
        bool PublishToAllSerial(string data, int serialPublishMaxWaitTime = 10000);
        void PublishToAll(string data);
    }
}
