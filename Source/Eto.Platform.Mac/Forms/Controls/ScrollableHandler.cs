// can't use flipped right now - bug in monomac/xam.mac that causes crashing since FlippedView gets disposed incorrectly
// has something to do with using layers (background colors) at the same time
//#define USE_FLIPPED

using System;
using SD = System.Drawing;
using Eto.Drawing;
using Eto.Forms;
using MonoMac.AppKit;
using MonoMac.Foundation;

namespace Eto.Platform.Mac.Forms.Controls
{
	public class ScrollableHandler : MacDockContainer<NSScrollView, Scrollable>, IScrollable
	{
		bool expandContentWidth = true;
		bool expandContentHeight = true;
		Point scrollPosition;

		public override NSView ContainerControl { get { return Control; } }

		class EtoScrollView : NSScrollView, IMacControl
		{
			public WeakReference WeakHandler { get; set; }

			public ScrollableHandler Handler
			{ 
				get { return (ScrollableHandler)WeakHandler.Target; }
				set { WeakHandler = new WeakReference(value); } 
			}

			public override void ResetCursorRects()
			{
				var cursor = Handler.Cursor;
				if (cursor != null)
					AddCursorRect(new SD.RectangleF(SD.PointF.Empty, Frame.Size), cursor.ControlObject as NSCursor);
			}

			#if !USE_FLIPPED
			public override void SetFrameSize(SD.SizeF newSize)
			{
				// keep the same position!
				var pos = Handler.ScrollPosition;
				base.SetFrameSize(newSize);
				var scrollSize = Handler.ScrollSize;
				var clientSize = Handler.ClientSize;
				pos.Y = Math.Max(0, Math.Min(pos.Y, scrollSize.Height - clientSize.Height));
				pos.X = Math.Max(0, Math.Min(pos.X, scrollSize.Width - clientSize.Width));
				Handler.ScrollPosition = pos;
				// since we override the base
				Handler.OnSizeChanged(EventArgs.Empty);
				Handler.Widget.OnSizeChanged(EventArgs.Empty);
			}
			#endif
		}
		#if USE_FLIPPED
		class FlippedView : NSView
		{
			public override bool IsFlipped
			{
				get { return true; }
			}
		}
		#endif
		public ScrollableHandler()
		{
			Enabled = true;
			Control = new EtoScrollView
			{
				Handler = this, 
				BackgroundColor = NSColor.Control,
				BorderType = NSBorderType.BezelBorder,
				DrawsBackground = false,
				HasVerticalScroller = true,
				HasHorizontalScroller = true,
				AutohidesScrollers = true,
				#if USE_FLIPPED
				DocumentView = new FlippedView()
				#else
				DocumentView = new NSView()
				#endif
			};

			// only draw dirty regions, instead of entire scroll area
			Control.ContentView.CopiesOnScroll = true;
		}

		public override void AttachEvent(string id)
		{
			switch (id)
			{
				#if !USE_FLIPPED
				case Eto.Forms.Control.SizeChangedEvent:
					// handled by delegate
					break;
				#endif
				case Scrollable.ScrollEvent:
					Control.ContentView.PostsBoundsChangedNotifications = true;
					AddObserver(NSView.BoundsChangedNotification, e =>
					{
						var w = (Scrollable)e.Widget;
						w.OnScroll(new ScrollEventArgs(w.ScrollPosition));
					}, Control.ContentView);
					break;
				default:
					base.AttachEvent(id);
					break;
			}
		}

		public BorderType Border
		{
			get
			{
				switch (Control.BorderType)
				{
					case NSBorderType.BezelBorder:
						return BorderType.Bezel;
					case NSBorderType.LineBorder:
						return BorderType.Line;
					case NSBorderType.NoBorder:
						return BorderType.None;
					default:
						throw new NotSupportedException();
				}
			}
			set
			{
				switch (value)
				{
					case BorderType.Bezel:
						Control.BorderType = NSBorderType.BezelBorder;
						break;
					case BorderType.Line:
						Control.BorderType = NSBorderType.LineBorder;
						break;
					case BorderType.None:
						Control.BorderType = NSBorderType.NoBorder;
						break;
					default:
						throw new NotSupportedException();
				}
			}
		}

		public override NSView ContentControl
		{
			get { return (NSView)Control.DocumentView; }
		}

		public override void LayoutChildren()
		{
			base.LayoutChildren();
			UpdateScrollSizes();
		}

		public override void OnLoadComplete(EventArgs e)
		{
			base.OnLoadComplete(e);
			ScrollPosition = scrollPosition;
		}

		Size GetBorderSize()
		{
			return Border == BorderType.None ? Size.Empty : new Size(2, 2);
		}

		protected override SizeF GetNaturalSize(SizeF availableSize)
		{
			return SizeF.Min(availableSize, base.GetNaturalSize(availableSize) + GetBorderSize());
		}

		protected override SD.RectangleF GetContentFrame()
		{
			var contentSize = Content.GetPreferredSize(SizeF.MaxValue);

			if (ExpandContentWidth)
				contentSize.Width = Math.Max(ClientSize.Width, contentSize.Width);
			if (ExpandContentHeight)
				contentSize.Height = Math.Max(ClientSize.Height, contentSize.Height);
			return new RectangleF(contentSize).ToSD();
		}

		#if !USE_FLIPPED
		protected override NSViewResizingMask ContentResizingMask()
		{
			return (NSViewResizingMask)0;
		}
		#endif

		void InternalSetFrameSize(SD.SizeF size)
		{
			var view = (NSView)Control.DocumentView;
			if (!view.IsFlipped)
			{
				var ctl = Content.GetContainerView();
				if (ctl != null)
				{
					var clientHeight = Control.DocumentVisibleRect.Size.Height;
					ctl.Frame = new SD.RectangleF(new SD.PointF(0, Math.Max(0, clientHeight - size.Height)), size);
					size.Height = Math.Max(clientHeight, size.Height);
				}
			}
			if (size != view.Frame.Size)
			{
				view.SetFrameSize(size);
			}
		}

		public void UpdateScrollSizes()
		{
			InternalSetFrameSize(GetContentFrame().Size);
		}

		public override Color BackgroundColor
		{
			get
			{
				return Control.BackgroundColor.ToEto();
			}
			set
			{
				Control.BackgroundColor = value.ToNS();
				Control.DrawsBackground = value.A > 0;
			}
		}

		public Point ScrollPosition
		{
			get
			{ 
				if (Widget.Loaded)
				{
					var view = (NSView)Control.DocumentView;
					var loc = Control.ContentView.Bounds.Location;
					if (view.IsFlipped)
						return loc.ToEtoPoint();
					else
						return new Point((int)loc.X, (int)(view.Frame.Height - Control.ContentView.Frame.Height - loc.Y));
				}
				else
					return scrollPosition;
			}
			set
			{ 
				if (Widget.Loaded)
				{
					var view = (NSView)Control.DocumentView;
					if (view.IsFlipped)
						Control.ContentView.ScrollToPoint(value.ToSDPointF());
					else
						Control.ContentView.ScrollToPoint(new SD.PointF(value.X, view.Frame.Height - Control.ContentView.Frame.Height - value.Y));
					Control.ReflectScrolledClipView(Control.ContentView);
				}
				else
					scrollPosition = value;
			}
		}

		public Size ScrollSize
		{			
			get { return ((NSView)Control.DocumentView).Frame.Size.ToEtoSize(); }
			set
			{ 
				InternalSetFrameSize(value.ToSDSizeF());
			}
		}

		public override Size ClientSize
		{
			get
			{
				return Control.DocumentVisibleRect.Size.ToEtoSize();
			}
			set
			{
				
			}
		}

		public override bool Enabled { get; set; }

		public override void SetContentSize(SD.SizeF contentSize)
		{
			if (MinimumSize != Size.Empty)
			{
				contentSize.Width = Math.Max(contentSize.Width, MinimumSize.Width);
				contentSize.Height = Math.Max(contentSize.Height, MinimumSize.Height);
			}
			if (ExpandContentWidth)
				contentSize.Width = Math.Max(ClientSize.Width, contentSize.Width);
			if (ExpandContentHeight)
				contentSize.Height = Math.Max(ClientSize.Height, contentSize.Height);
			InternalSetFrameSize(contentSize);
		}

		public Rectangle VisibleRect
		{
			get { return new Rectangle(ScrollPosition, Size.Min(ScrollSize, ClientSize)); }
		}

		public bool ExpandContentWidth
		{
			get { return expandContentWidth; }
			set
			{
				if (expandContentWidth != value)
				{
					expandContentWidth = value;
					UpdateScrollSizes();
				}
			}
		}

		public bool ExpandContentHeight
		{
			get { return expandContentHeight; }
			set
			{
				if (expandContentHeight != value)
				{
					expandContentHeight = value;
					UpdateScrollSizes();
				}
			}
		}
	}
}
