using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusterWood.Msmq
{
    public enum BodyType
    {
        None = 0,
        ByteArray = MessageProperties.VT_VECTOR | MessageProperties.VT_I1,
        AnsiString = MessageProperties.VT_LPSTR,
        UnicodeString = MessageProperties.VT_LPWSTR,
    }
}
