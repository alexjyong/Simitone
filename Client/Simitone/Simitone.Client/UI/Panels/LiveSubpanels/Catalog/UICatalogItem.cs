using FSO.Client;
using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using FSO.Content;
using FSO.Files;
using FSO.Files.Formats.IFF.Chunks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simitone.Client.UI.Controls;
using Simitone.Client.UI.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Simitone.Client.UI.Panels.LiveSubpanels.Catalog
{
    public class UICatalogItem : UITSContainer
    {
        public static Dictionary<uint, Texture2D> IconCache = new Dictionary<uint, Texture2D>();

        // Background icon loading: BMP byte data loaded off-thread, waiting for texture creation on main thread
        private static ConcurrentDictionary<uint, byte[]> _pendingIcons = new ConcurrentDictionary<uint, byte[]>();
        private static ConcurrentDictionary<uint, byte> _loadingIcons = new ConcurrentDictionary<uint, byte>();

        public Texture2D BG;
        public Texture2D Icon;
        public Texture2D Outline;
        public bool Outlined;
        private uint _iconGUID;
        private bool _isSpecialIcon;

        public UILabel PriceLabel;
        private UIBuyBrowsePanel BudgetProvider;

        public override void Draw(UISpriteBatch SBatch)
        {
            // Check if our icon arrived from background loading
            if (!_isSpecialIcon && Icon == null && _iconGUID != 0)
            {
                Icon = GetOrCreateIcon(_iconGUID);
            }

            DrawLocalTexture(SBatch, BG, null, new Vector2(BG.Width-90, BG.Height-105) / -2, Vector2.One, new Color(104, 164, 184, 255));
            var iconSize = 55f;
            if (Icon != null)
            {
                
                if (Icon.Width / (float)Icon.Height < 1.1f || Icon.Width == 127 || Icon.Width == 128)
                {
                    iconSize = 77.7f;
                    var scale = iconSize/(float)Math.Sqrt(Icon.Width * Icon.Width + Icon.Height * Icon.Height);
                    DrawLocalTexture(SBatch, Icon, new Rectangle(0, 0, Icon.Width, Icon.Height), new Vector2((Icon.Width*scale-90) / -2, (Icon.Height*scale-105) / -2), new Vector2(scale));
                }
                else DrawLocalTexture(SBatch, Icon, new Rectangle(0, 0, Icon.Width / 2, Icon.Height), new Vector2((iconSize-90) / -2, (iconSize- 105) / -2), new Vector2(iconSize / Icon.Height, iconSize / Icon.Height));
            }

            if (Outlined) DrawLocalTexture(SBatch, Outline, null, new Vector2(Outline.Width - 90, Outline.Height - 105) / -2, Vector2.One, UIStyle.Current.ActiveSelection);
            base.Draw(SBatch);
        }

        public UICatalogItem(UICatalogElement elem, UIBuyBrowsePanel budgetProvider)
        {
            BG = Content.Get().CustomUI.Get("pswitch_icon_bg.png").Get(GameFacade.GraphicsDevice);
            Outline = Content.Get().CustomUI.Get("pswitch_icon_sel.png").Get(GameFacade.GraphicsDevice);

            if (elem.Special?.Res != null)
            {
                Icon = elem.Special.Res.GetIcon(elem.Special.ResID);
                _isSpecialIcon = true;
            }
            else
            {
                _iconGUID = elem.Item.GUID;
                Icon = GetOrCreateIcon(_iconGUID);
            }

            PriceLabel = new UILabel();
            PriceLabel.Alignment = TextAlignment.Center | TextAlignment.Middle;
            PriceLabel.Position = new Vector2(0, 110);
            PriceLabel.Size = new Vector2(90, 1);
            PriceLabel.CaptionStyle = PriceLabel.CaptionStyle.Clone();
            PriceLabel.CaptionStyle.Color = UIStyle.Current.Text;
            PriceLabel.CaptionStyle.Size = 14;
            PriceLabel.Caption = "§" + elem.Item.Price.ToString();
            Add(PriceLabel);

            BudgetProvider = budgetProvider;
        }

        public override void Selected()
        {
            if (BudgetProvider.ItemID == ItemID)
            {
                Outlined = false;
                BudgetProvider.Deselect();
            }
            else
            {
                Outlined = true;
                BudgetProvider.Selected(ItemID);
            }
        }

        public override void Deselected()
        {
            Outlined = false;
        }

        /// <summary>
        /// Returns cached icon texture if available. If not cached, queues background loading
        /// and returns null (icon will appear on a subsequent frame).
        /// </summary>
        private static Texture2D GetOrCreateIcon(uint GUID)
        {
            // Already have a texture cached
            if (IconCache.TryGetValue(GUID, out var cached))
                return cached;

            // BMP data arrived from background thread — create texture on main thread
            if (_pendingIcons.TryRemove(GUID, out var bmpData))
            {
                Texture2D tex = null;
                if (bmpData != null)
                {
                    try
                    {
                        tex = ImageLoader.FromStream(GameFacade.GraphicsDevice, new MemoryStream(bmpData));
                    }
                    catch { }
                }
                IconCache[GUID] = tex;
                return tex;
            }

            // Not loading yet — queue background load
            if (_loadingIcons.TryAdd(GUID, 0))
            {
                ThreadPool.QueueUserWorkItem(_ => LoadIconBackground(GUID));
            }

            return null;
        }

        /// <summary>
        /// Runs on a background thread: loads the IFF and extracts BMP byte data.
        /// </summary>
        private static void LoadIconBackground(uint GUID)
        {
            try
            {
                var obj = Content.Get().WorldObjects.Get(GUID);
                if (obj == null)
                {
                    _pendingIcons[GUID] = null;
                    return;
                }
                var bmp = obj.Resource.Get<BMP>(obj.OBJ.CatalogStringsID);
                _pendingIcons[GUID] = bmp?.data;
            }
            catch
            {
                _pendingIcons[GUID] = null;
            }
            finally
            {
                _loadingIcons.TryRemove(GUID, out _);
            }
        }

        public static void ClearIconCache()
        {
            foreach (var item in IconCache)
            {
                item.Value?.Dispose();
            }
            IconCache.Clear();
            _pendingIcons.Clear();
            _loadingIcons.Clear();
        }
    }
}
