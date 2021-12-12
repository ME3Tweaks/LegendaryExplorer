using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LegendaryExplorer.UserControls.Interfaces
{
    /// <summary>
    /// Classes implementing this interface will have the properties that can be binded to by the SceneControlOptionsControl. Set the data context of that control to the one implementing this.
    /// </summary>
    interface ISceneRenderContextConfigurable
    {
        public bool SetAlphaToBlack { get; set; }
        public bool ShowRedChannel { get; set; }
        public bool ShowGreenChannel { get; set; }
        public bool ShowBlueChannel { get; set; }
        public bool ShowAlphaChannel { get; set; }
        public System.Windows.Media.Color BackgroundColor { get; set; }
    }
}
