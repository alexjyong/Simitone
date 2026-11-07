using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Content;
using Simitone.Client.UI.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simitone.Client.UI.Controls
{
    public class UIBigButton : UIButton
    {
        public UIBigButton(bool green) : base()
        {
            CaptionStyle = CaptionStyle.Clone();
            CaptionStyle.Size = 37;
            CaptionStyle.Color = (green)?UIStyle.Current.GreenBtnTxt:UIStyle.Current.BtnTxt;
            CaptionStyle.DisabledColor = UIStyle.Current.BtnDisable;
            // the default hover/pressed caption colour is white, which vanishes on these white buttons
            // (the texture already has its own hover and pressed frames)
            CaptionStyle.HighlightedColor = CaptionStyle.Color;
            CaptionStyle.SelectedColor = CaptionStyle.Color;
            Texture = Content.Get().CustomUI.Get(green ? "greenbutton.png" : "button.png").Get(GameFacade.GraphicsDevice);
        }
    }
}
