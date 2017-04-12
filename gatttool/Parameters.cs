using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace gatttool
{
    class Parameters
    {
        public string DeviceMac
        { get; set; }
        public string CharacteristicHandle
        { get; set; }
        public bool Read
        { get; set; }
        public bool Write
        { get; set; }
        public bool Characteristics
        { get; set; }
        public string WriteValue
        { get; set; }
    }
}
