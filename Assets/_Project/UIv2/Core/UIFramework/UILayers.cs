using System;
using UnityEngine;

namespace ProjectC.UIv2.Core.UIFramework
{
    public abstract class UILayerBase : IUILayer
    {
        public abstract string LayerId { get; }
        public abstract int Priority { get; }

        public virtual void Mount(IUIScreen screen) { }
        public virtual void Unmount(IUIScreen screen) { }
        public virtual void SetInputEnabled(bool enabled) { }
        public virtual bool ClaimsInput(string action) => false;
    }

    public class HudLayer : UILayerBase
    {
        public override string LayerId => "HUD";
        public override int Priority => 20;
    }

    public class ModalLayer : UILayerBase
    {
        public override string LayerId => "Modal";
        public override int Priority => 40;
    }

    public class TooltipLayer : UILayerBase
    {
        public override string LayerId => "Tooltip";
        public override int Priority => 30;
    }

    public class OverlayLayer : UILayerBase
    {
        public override string LayerId => "Overlay";
        public override int Priority => 50;
    }
}
