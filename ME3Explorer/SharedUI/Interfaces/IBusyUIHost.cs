using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Explorer.SharedUI.Interfaces
{
    public interface IBusyUIHost
    {
        bool IsBusy { get;set; }
        string BusyText { get; set; }
    }
}
