using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlaythingADB.Models
{
    public class OverlayModel
    {
        public string Name { get; set; }
        public bool IsChecked { get; set; }
        public bool IsEnable { get; set; }
    }

    public class OverlayGroup
    {
        public string GroupName { get; set; }
        public List<OverlayModel> Overlays { get; set; } = new();
    }

    public class AppOverlay
    {
        public string AppName { get; set; }
        public List<OverlayGroup> OverlayGroups { get; set; } = new();
    }
}
