﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.IO;
using System.Windows.Threading;
using System.Diagnostics;
using System.Threading;

namespace LongBar
{
	/// <summary>
	/// Interaction logic for LongBar.xaml
	/// </summary>
	public partial class LongBarMain : Window
	{
		[DllImport("user32.dll")]
		public static extern IntPtr SendMessageW(IntPtr hWnd, UInt32 msg, UInt32 wParam, IntPtr lParam);
		[DllImport("user32.dll")]
		private static extern int FindWindowW(string className, string windowName);
		[DllImport("gdi32.dll")]
		static extern IntPtr CreateRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect);

		public IntPtr Handle;
		private Options options;
		private string path = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
		public static List<Tile> Tiles = new List<Tile>();

		public Shadow shadow = new Shadow();
		private Library library;
		public static Slate.Options.Settings sett = Slate.Options.SettingsManager.DefaultSettings;
		public static Slate.Options.StartupFlags flags = new Slate.Options.StartupFlags();
		private Window dummyWin;

		public LongBarMain()
		{
			InitializeComponent();
			options = new Options(this);
			this.Closed += new EventHandler(LongBar_Closed);
			this.SourceInitialized += new EventHandler(LongBar_SourceInitialized);
			this.ContentRendered += new EventHandler(LongBar_ContentRendered);
			this.MouseMove += new MouseEventHandler(LongBar_MouseMove);
			this.MouseDoubleClick += new MouseButtonEventHandler(LongBar_MouseDoubleClick);
			this.DragEnter += new DragEventHandler(LongBar_DragEnter);
			this.Drop += new DragEventHandler(LongBar_Drop);
		}

		private void LongBar_Closed(object sender, EventArgs e)
		{
			shadow.Close();

			if (Slate.General.Sidebar.Overlapped) //&& sett.side == Slate.General.Sidebar.Side.Right)
			{
				Slate.General.Sidebar.UnOverlapTaskbar();
			}
			Slate.General.SystemTray.RemoveIcon();
			Slate.General.Sidebar.AppbarRemove();

			WriteSettings();

			RoutedEventArgs args = new RoutedEventArgs(UserControl.UnloadedEvent);
			foreach (Tile tile in TilesGrid.Children)
			{
			    tile.RaiseEvent(args);
			}
			TilesGrid.Children.Clear();
		}

		private void LongBar_SourceInitialized(object sender, EventArgs e)
		{
			// Create Dummy Window
			dummyWin = new Window() { ShowInTaskbar = false, WindowStyle = System.Windows.WindowStyle.ToolWindow,
										Width = 0, Height = 0, Top = -100, Left = -100, Visibility = Visibility.Hidden };
			dummyWin.Show();
			dummyWin.Hide();
			Owner = shadow.Owner = dummyWin;

			Handle = new WindowInteropHelper(this).Handle;
			ReadSettings();
			Slate.Themes.ThemesManager.LoadTheme(sett.Program.Path, sett.Program.Theme);
			object enableGlass = Slate.Themes.ThemesManager.GetThemeParameter(sett.Program.Path, sett.Program.Theme, "boolean", "EnableGlass");
			if (enableGlass != null && !Convert.ToBoolean(enableGlass))
			    sett.Program.EnableGlass = false;
			object useSystemColor = Slate.Themes.ThemesManager.GetThemeParameter(sett.Program.Path, sett.Program.Theme, "boolean", "UseSystemGlassColor");

			if (useSystemColor != null && Convert.ToBoolean(useSystemColor))
			{
			    int color;
			    bool opaque;
			    Slate.DWM.DwmManager.DwmGetColorizationColor(out color, out opaque);
			    Bg.Fill = new SolidColorBrush(Color.FromArgb(System.Drawing.Color.FromArgb(color).A, System.Drawing.Color.FromArgb(color).R, System.Drawing.Color.FromArgb(color).G, System.Drawing.Color.FromArgb(color).B));
			    Slate.General.Sidebar.DwmColorChanged += new EventHandler(SideBar_DwmColorChanged);
			}

			Slate.Localization.LocaleManager.LoadLocale(sett.Program.Path, sett.Program.Language);

			this.Width = sett.Program.Width;
			Slate.General.SystemTray.AddIcon(this);
			Slate.General.Sidebar.SetSidebar(this, sett.Program.Side, false, sett.Program.OverlapTaskbar, sett.Program.Screen);
			SetSide(sett.Program.Side);
			this.MaxWidth = SystemParameters.PrimaryScreenWidth / 2;
			this.MinWidth = 31;

			Slate.DWM.DwmManager.RemoveFromFlip3D(Handle);
			Slate.DWM.DwmManager.RemoveFromAeroPeek(Handle);
			
			Slate.General.SystemTray.SidebarvisibleChanged += new Slate.General.SystemTray.SidebarvisibleChangedEventHandler(SystemTray_SidebarvisibleChanged);

			GetTiles();
		}

	void SystemTray_SidebarvisibleChanged(bool value)
	{
		if (value)
			shadow.Visibility = Visibility.Visible;
		else
			shadow.Visibility = Visibility.Collapsed;
	}

	void SideBar_DwmColorChanged(object sender, EventArgs e)
	{
		int color;
		bool opaque;
		Slate.DWM.DwmManager.DwmGetColorizationColor(out color, out opaque);
		Bg.Fill = new SolidColorBrush(Color.FromArgb(System.Drawing.Color.FromArgb(color).A, System.Drawing.Color.FromArgb(color).R, System.Drawing.Color.FromArgb(color).G, System.Drawing.Color.FromArgb(color).B));

	}

	private void LongBar_ContentRendered(object sender, EventArgs e)
	{
	  OpacityMaskGradStop.BeginAnimation(GradientStop.OffsetProperty, ((DoubleAnimation)this.Resources["LoadAnimOffset"]));
	  OpacityMaskGradStop1.BeginAnimation(GradientStop.OffsetProperty, ((DoubleAnimation)this.Resources["LoadAnimOffset1"]));
	  this.BeginAnimation(UIElement.OpacityProperty, ((DoubleAnimation)this.Resources["DummyAnim"]));
	}

	private void LoadAnimation_Completed(object sender, EventArgs e)
	{
	  if (Slate.DWM.DwmManager.IsGlassAvailable() && sett.Program.EnableGlass)
		Slate.DWM.DwmManager.EnableGlass(ref Handle, IntPtr.Zero);

	  shadow.Height = this.Height;
	  shadow.Top = this.Top;

	  if (sett.Program.EnableShadow)
	  {
		  shadow.Show();
	  }

	  if (sett.Program.EnableSnowFall)
	  {
		  EnableSnowFall();
	  }

	  if (sett.Program.EnableUpdates)
	  {

		  foreach (string file in Directory.GetFiles(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "*.old", SearchOption.TopDirectoryOnly))
		  {
			  File.Delete(file);
		  }

		  foreach (string file in Directory.GetFiles(sett.Program.Path, "*.old", SearchOption.AllDirectories))
		  {
			  File.Delete(file);
		  }

		  ThreadStart threadStarter = delegate
		  {
			  Slate.Updates.UpdatesManager.UpdateInfo updateInfo = Slate.Updates.UpdatesManager.CheckForUpdates(Assembly.GetExecutingAssembly().GetName().Version.Build);
			  if (updateInfo.Build != null && updateInfo.Description != null)
			  {
				  TaskDialogs.UpdateDialog.ShowDialog(updateInfo.Build, updateInfo.Description);
			  }
		  };
		  Thread thread = new Thread(threadStarter);
		  thread.SetApartmentState(ApartmentState.STA);
		  thread.Start();
	  }
	}

	private void DummyAnimation_Completed(object sender, EventArgs e)
	{
		LoadTilesAtStartup();
	}

	private void LoadTilesAtStartup()
	{
		try {
			if (!flags.Debug)
	        {
				if (sett.Program.Tiles != null && Tiles != null && sett.Program.Tiles.Length > 0 && Tiles.Count > 0)
	            {
					for (int i = 0; i < sett.Program.Tiles.Length; i++)
	                {
						string tileName = sett.Program.Tiles[i];
	                    foreach (Tile tile in Tiles)
	                    {
	                        if (tile.File.Substring(tile.File.LastIndexOf(@"\") + 1) == tileName)
	                        {
	                            double tileHeight = double.NaN;
								if (sett.Program.Heights != null && sett.Program.Heights.Length > i)
	                            {
									if (sett.Program.Heights[i].EndsWith("M"))
	                                {
										tileHeight = double.Parse(sett.Program.Heights[i].Replace("M", string.Empty));
	                                    tile.minimized = true;
	                                }
	                                else
										tileHeight = double.Parse(sett.Program.Heights[i]);
	                            }
	                            if (!double.IsNaN(tileHeight))
									tile.Load(sett.Program.Side, tileHeight);
	                            else
									tile.Load(sett.Program.Side, double.NaN);
	                            if (!tile.hasErrors)
	                                TilesGrid.Children.Add(tile);
	                        }
	                    }
	                }
	            }

				if (sett.Program.PinnedTiles != null && Tiles != null && sett.Program.PinnedTiles.Length > 0 && Tiles.Count > 0)
	            {
					for (int i = 0; i < sett.Program.PinnedTiles.Length; i++)
	                {
	                    foreach (Tile tile in Tiles)
	                    {
							if (tile.File.EndsWith(sett.Program.PinnedTiles[i]))
	                        {
	                            tile.pinned = true;
								tile.Load(sett.Program.Side, double.NaN);

	                            tile.Header.Visibility = System.Windows.Visibility.Collapsed;
	                            DockPanel.SetDock(tile.Splitter, Dock.Top);
	                            ((MenuItem)tile.ContextMenu.Items[0]).IsChecked = true;

	                            if (!tile.hasErrors)
	                            {
	                                PinGrid.Children.Add(tile);
	                            }
	                        }
	                    }
	                }
	            }
	        }
	        else
	        {
	            if (Tiles.Count > 0)
	            {
					Tiles[0].Load(sett.Program.Side, double.NaN);
	                TilesGrid.Children.Add(Tiles[0]);
	            }
	        }
	        } catch (Exception) {
				// FIXME: Temporary fix, supresses the error - issue #9
		}
	}

	private void GetTiles()
	{
		if (!flags.Debug)
		{
			if (System.IO.Directory.Exists(sett.Program.Path + @"\Library"))
				foreach (string dir in System.IO.Directory.GetDirectories(sett.Program.Path + @"\Library"))
				{
					string file = string.Format(@"{0}\{1}.dll", dir, System.IO.Path.GetFileName(dir));
					if (System.IO.File.Exists(file))
					{
						Tiles.Add(new Tile(file));
						if (Tiles[Tiles.Count - 1].hasErrors)
							Tiles.RemoveAt(Tiles.Count - 1);
						else
						{
							MenuItem item = new MenuItem();
							if (Tiles[Tiles.Count - 1].Info != null)
								item.Header = Tiles[Tiles.Count - 1].Info.Name;
							item.Click += new RoutedEventHandler(AddTileSubItem_Click);
							Image icon = new Image();
							icon.Source = Tiles[Tiles.Count - 1].TitleIcon.Source;
							icon.Width = 25;
							icon.Height = 25;
							item.Icon = icon;
							AddTileItem.Items.Add(item);
						}
					}
				}
		}
		else
		{
			Tiles.Add(new Tile(flags.TileToDebug));
			if (Tiles[Tiles.Count - 1].hasErrors)
				Tiles.RemoveAt(Tiles.Count - 1);
			else
			{
				MenuItem item = new MenuItem();
				if (Tiles[Tiles.Count - 1].Info != null)
				item.Header = Tiles[Tiles.Count - 1].Info.Name;
				item.Click += new RoutedEventHandler(AddTileSubItem_Click);
				Image icon = new Image();
				icon.Source = Tiles[Tiles.Count - 1].TitleIcon.Source;
				icon.Width = 25;
				icon.Height = 25;
				item.Icon = icon;
				AddTileItem.Items.Add(item);
			}
		}
	}

	public void AddTileSubItem_Click(object sender, RoutedEventArgs e)
	{
	  int index = AddTileItem.Items.IndexOf(sender);
	  if (!((MenuItem)AddTileItem.Items[index]).IsChecked)
	  {
		  Tiles[index].Load(sett.Program.Side, double.NaN);
		if (!Tiles[index].hasErrors)
		{
			TilesGrid.Children.Insert(0, Tiles[index]);
			((MenuItem)AddTileItem.Items[index]).IsChecked = true;
		}
	  }
	  else
	  {
		Tiles[index].Unload();
		((MenuItem)AddTileItem.Items[index]).IsChecked = false;
	  }
	}

	public static void ReadSettings()
	{
		sett = Slate.Options.SettingsManager.UserSettings;
	}

	private void WriteSettings()
	{
	  sett.Program.Width = (int)this.Width;

	  sett.Program.Tiles = new string[TilesGrid.Children.Count];
	  sett.Program.Heights = new string[TilesGrid.Children.Count];

	  if (TilesGrid.Children.Count > 0)
	  {
		  for (int i = 0; i < TilesGrid.Children.Count; i++)
		  {
			  sett.Program.Tiles[i] = System.IO.Path.GetFileName(Tiles[Tiles.IndexOf(((Tile)TilesGrid.Children[i]))].File);
			  if (Tiles[Tiles.IndexOf(((Tile)TilesGrid.Children[i]))].minimized)
				  sett.Program.Heights[i] = Tiles[Tiles.IndexOf(((Tile)TilesGrid.Children[i]))].normalHeight.ToString() + "M";
			  else
				  sett.Program.Heights[i] = Tiles[Tiles.IndexOf(((Tile)TilesGrid.Children[i]))].Height.ToString();
		  }
	  }

	  if (PinGrid.Children.Count > 0)
	  {
		  sett.Program.PinnedTiles = new string[PinGrid.Children.Count];

		  for (int i = 0; i < PinGrid.Children.Count; i++)
		  {
			  sett.Program.PinnedTiles[i] = System.IO.Path.GetFileName(Tiles[Tiles.IndexOf(((Tile)PinGrid.Children[i]))].File);
		  }
	  }

	  // Finally, save the file
	  Slate.Options.SettingsManager.Save<Slate.Options.Settings>(sett, "Settings.xml");
	}

	private void LongBar_MouseMove(object sender, MouseEventArgs e)
	{
		switch (sett.Program.Side)
	  {
		case Slate.General.Sidebar.Side.Right:
		  if (e.GetPosition(this).X <= 5 && !sett.Program.Locked)
		  {
			base.Cursor = Cursors.SizeWE;
			if (e.LeftButton == MouseButtonState.Pressed)
			{
			  SendMessageW(Handle, 274, 61441, IntPtr.Zero);
			  sett.Program.Width = (int)this.Width;
			  if (sett.Program.TopMost)
			  {
				shadow.Topmost = true;
				Slate.General.Sidebar.SizeAppbar();
			  } else {
				shadow.Topmost = false;
				  Slate.General.Sidebar.SetPos();
			  }
			}
		  }
		  else if (base.Cursor != Cursors.Arrow)
			base.Cursor = Cursors.Arrow;
		  break;
		case Slate.General.Sidebar.Side.Left:
		  if (e.GetPosition(this).X >= this.Width - 5 && !sett.Program.Locked)
		  {
			base.Cursor = Cursors.SizeWE;
			if (e.LeftButton == MouseButtonState.Pressed)
			{
			  SendMessageW(Handle, 274, 61442, IntPtr.Zero);
			  sett.Program.Width = (int)this.Width;
			  if (sett.Program.TopMost)
			  {
				shadow.Topmost = true;
				Slate.General.Sidebar.SizeAppbar();
			  } else {
				shadow.Topmost = false;
				  Slate.General.Sidebar.SetPos();
			  }
			}
		  }
		  else if (base.Cursor != Cursors.Arrow)
			base.Cursor = Cursors.Arrow;
		  break;
	  }
	}

	private void LongBar_MouseDoubleClick(object sender, MouseButtonEventArgs e)
	{
	  switch (sett.Program.Side)
	  {
		  case Slate.General.Sidebar.Side.Right:
		  if (e.GetPosition(this).X <= 5 && !sett.Program.Locked)
		  {
			this.Width = 150;
			  if (sett.Program.TopMost) {
				shadow.Topmost = true;
				Slate.General.Sidebar.SizeAppbar();
			  } else {
				shadow.Topmost = false;
				  Slate.General.Sidebar.SetPos();
			  }
			shadow.Left = this.Left - shadow.Width;
		  }
		  break;
		  case Slate.General.Sidebar.Side.Left:
		  if (e.GetPosition(this).X >= this.Width - 5 && !sett.Program.Locked)
		  {
			this.Width = 150;
			  if (sett.Program.TopMost) {
				shadow.Topmost = true;
				Slate.General.Sidebar.SizeAppbar();
			  } else {
				shadow.Topmost = false;
				  Slate.General.Sidebar.SetPos();
			  }
			shadow.Left = this.Left + this.Width;
		  }
		  break;
	  }
	  if (Keyboard.IsKeyDown(Key.LeftShift))
		  ShowNotification();
	}

	private void DropDownMenu_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
	  Menu.IsOpen = true;
	}

	private void CloseButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
	  this.Close();
	}

	private void CloseItem_Click(object sender, RoutedEventArgs e)
	{
	  this.Close();
	}

	private void LockItem_Checked(object sender, RoutedEventArgs e)
	{
		sett.Program.Locked = true;
	}

	private void LockItem_Unchecked(object sender, RoutedEventArgs e)
	{
		sett.Program.Locked = false;
	}
	public void SetSide(Slate.General.Sidebar.Side side)
	{
	  switch (side)
	  {
		case Slate.General.Sidebar.Side.Left:
			  Slate.General.Sidebar.SetSidebar(this, Slate.General.Sidebar.Side.Left, sett.Program.TopMost, sett.Program.OverlapTaskbar, sett.Program.Screen);
		   Bg.FlowDirection = FlowDirection.RightToLeft;
		   BgHighlight.FlowDirection = FlowDirection.RightToLeft;
		   BgHighlight.HorizontalAlignment = HorizontalAlignment.Right;
		   Highlight.FlowDirection = FlowDirection.RightToLeft;
		   Highlight.HorizontalAlignment = HorizontalAlignment.Right;

		   shadow.Left = this.Left + this.Width;
		   shadow.FlowDirection = FlowDirection.RightToLeft;

		  foreach (Tile tile in TilesGrid.Children)
			  tile.ChangeSide(Slate.General.Sidebar.Side.Left);
		  break;
		case Slate.General.Sidebar.Side.Right:
		  Slate.General.Sidebar.SetSidebar(this, Slate.General.Sidebar.Side.Right, sett.Program.TopMost, sett.Program.OverlapTaskbar, sett.Program.Screen);
		  Bg.FlowDirection = FlowDirection.LeftToRight;
		  BgHighlight.FlowDirection = FlowDirection.LeftToRight;
		  BgHighlight.HorizontalAlignment = HorizontalAlignment.Left;
		  Highlight.FlowDirection = FlowDirection.LeftToRight;
		  Highlight.HorizontalAlignment = HorizontalAlignment.Left;

		  shadow.Left = this.Left - shadow.Width;
		  shadow.FlowDirection = FlowDirection.LeftToRight;
		  if (sett.Program.TopMost) { shadow.Topmost = true; } else { shadow.Topmost = false; }

		  foreach (Tile tile in TilesGrid.Children)
			  tile.ChangeSide(Slate.General.Sidebar.Side.Right);
		  break;
		// FIXME: Under-implemented top/left sides
		case Slate.General.Sidebar.Side.Top:
		  Slate.General.Sidebar.SetSidebar(this, Slate.General.Sidebar.Side.Top, sett.Program.TopMost, false, sett.Program.Screen);
		  Bg.FlowDirection = FlowDirection.LeftToRight;
		  BgHighlight.FlowDirection = FlowDirection.LeftToRight;
		  BgHighlight.HorizontalAlignment = HorizontalAlignment.Left;
		  Highlight.FlowDirection = FlowDirection.LeftToRight;
		  Highlight.HorizontalAlignment = HorizontalAlignment.Left;

		  shadow.Left = this.Left - shadow.Width;
		  shadow.FlowDirection = FlowDirection.LeftToRight;
		  if (sett.Program.TopMost) { shadow.Topmost = true; } else { shadow.Topmost = false; }

		  foreach (Tile tile in TilesGrid.Children)
			  tile.ChangeSide(Slate.General.Sidebar.Side.Right);
		  break;
		case Slate.General.Sidebar.Side.Bottom:
		  Slate.General.Sidebar.SetSidebar(this, Slate.General.Sidebar.Side.Bottom, sett.Program.TopMost, false, sett.Program.Screen);
		  Bg.FlowDirection = FlowDirection.LeftToRight;
		  BgHighlight.FlowDirection = FlowDirection.LeftToRight;
		  BgHighlight.HorizontalAlignment = HorizontalAlignment.Left;
		  Highlight.FlowDirection = FlowDirection.LeftToRight;
		  Highlight.HorizontalAlignment = HorizontalAlignment.Left;

		  shadow.Left = this.Left - shadow.Width;
		  shadow.FlowDirection = FlowDirection.LeftToRight;
		  if (sett.Program.TopMost) { shadow.Topmost = true; } else { shadow.Topmost = false; }

		  foreach (Tile tile in TilesGrid.Children)
			  tile.ChangeSide(Slate.General.Sidebar.Side.Right);
		  break;
	  }
	}

	public void SetLocale(string locale)
	{
		Slate.Localization.LocaleManager.LoadLocale(sett.Program.Path, locale);
		Slate.General.SystemTray.SetLocale();
		foreach (Tile tile in TilesGrid.Children)
		  tile.ChangeLocale(locale);
	}

	public void SetTheme(string theme)
	{
		Slate.Themes.ThemesManager.LoadTheme(sett.Program.Path, theme);

		object useSystemColor = Slate.Themes.ThemesManager.GetThemeParameter(sett.Program.Path, sett.Program.Theme, "boolean", "UseSystemGlassColor");
		if (useSystemColor != null && Convert.ToBoolean(useSystemColor))
		{
			int color;
			bool opaque;
			Slate.DWM.DwmManager.DwmGetColorizationColor(out color, out opaque);
			//HwndSource.FromHwnd(Handle).CompositionTarget.BackgroundColor = Color.FromArgb(System.Drawing.Color.FromArgb(color).A,System.Drawing.Color.FromArgb(color).R,System.Drawing.Color.FromArgb(color).G,System.Drawing.Color.FromArgb(color).B);
			Bg.Fill = new SolidColorBrush(Color.FromArgb(System.Drawing.Color.FromArgb(color).A, System.Drawing.Color.FromArgb(color).R, System.Drawing.Color.FromArgb(color).G, System.Drawing.Color.FromArgb(color).B));
			Slate.General.Sidebar.DwmColorChanged += new EventHandler(SideBar_DwmColorChanged);
		}
		else
		{
			Bg.SetResourceReference(Rectangle.StyleProperty, "Background");
			Slate.General.Sidebar.DwmColorChanged -= new EventHandler(SideBar_DwmColorChanged);
		}

		string file = string.Format(@"{0}\{1}.theme.xaml", sett.Program.Path, theme);

		foreach (Tile tile in TilesGrid.Children)
			tile.ChangeTheme(file);
	}

	private void LockItem_Click(object sender, RoutedEventArgs e)
	{
	  if (sett.Program.Locked)
	  {
		LockItem.IsChecked = false;
		LockItem.Header = TryFindResource("Lock");
		sett.Program.Locked = false;
	  }
	  else
	  {
		LockItem.IsChecked = true;
		LockItem.Header = TryFindResource("Lock");
		sett.Program.Locked = true;
	  }
	}

	private void SettingsItem_Click(object sender, RoutedEventArgs e)
	{
	  if (options.IsVisible)
	  {
		options.Activate();
		return;
	  }
	  options = new Options(this);
	  options.ShowDialog();
	}

	private void Menu_Opened(object sender, RoutedEventArgs e)
	{
		if (sett.Program.Locked)
		{
		LockItem.IsChecked = true;
		LockItem.Header = TryFindResource("Lock");
		} else {
		LockItem.IsChecked = false;
		LockItem.Header = TryFindResource("Lock");
		}
	  if (TilesGrid.Children.Count == 0)
		  RemoveTilesItem.IsEnabled = false;
	  else
		  RemoveTilesItem.IsEnabled = true;

	  if (System.IO.Directory.Exists(sett.Program.Path + @"\Library") && Tiles.Count != System.IO.Directory.GetDirectories(sett.Program.Path + @"\Library").Length)
		foreach (string d in System.IO.Directory.GetDirectories(sett.Program.Path + @"\Library"))
		{
		  string file = string.Format(@"{0}\{1}.dll", d, System.IO.Path.GetFileName(d));
		  if(!CheckTileAdded(file))
			if (System.IO.File.Exists(file))
			{
			  Tiles.Add(new Tile(file));
			  if (Tiles[Tiles.Count - 1].hasErrors)
				Tiles.RemoveAt(Tiles.Count - 1);
			  else
			  {
				MenuItem item = new MenuItem();
				if (Tiles[Tiles.Count - 1].Info != null)
				  item.Header = Tiles[Tiles.Count - 1].Info.Name;
				item.Click += new RoutedEventHandler(AddTileSubItem_Click);
				AddTileItem.Items.Add(item);
			  }
			}
		}
	  for (int i = 0; i < Tiles.Count; i++)
		if (Tiles[i].isLoaded)
		  ((MenuItem)AddTileItem.Items[i]).IsChecked = true;
		else
		  ((MenuItem)AddTileItem.Items[i]).IsChecked = false;
	  if (AddTileItem.Items.Count > 0)
		AddTileItem.IsEnabled = true;
	  else
		AddTileItem.IsEnabled = false;
	}

	private bool CheckTileAdded(string file)
	{
	  foreach (Tile tile in Tiles)
		if (tile.File == file)
		  return true;
	  return false;
	}

	private void MinimizeItem_Click(object sender, RoutedEventArgs e)
	{
		if (!Slate.General.SystemTray.SidebarVisible)
			Slate.General.SystemTray.SidebarVisible = true;
		else Slate.General.SystemTray.SidebarVisible = false;
	}

	private void LongBar_DragEnter(object sender, DragEventArgs e)
	{
	  if(e.Data.GetDataPresent(DataFormats.FileDrop))
		e.Effects = DragDropEffects.Copy;
	}

	private void LongBar_Drop(object sender, DragEventArgs e)
	{
	  string[] files = (string[])e.Data.GetData(DataFormats.FileDrop, true);
	  if(files.Length>0)
		  for (int i = 0; i < files.Length; i++)
		  {
			  if (files[i].EndsWith(".tile"))
			  {
				  FileInfo info = new FileInfo(files[i]);
				  TaskDialogs.TileInstallDialog.ShowDialog(this, info.Name, files[i]);
			  }
			  if (files[i].EndsWith(".locale.xaml"))
			  {
				  if (Slate.Localization.LocaleManager.InstallLocale(sett.Program.Path, files[i]))
				  {
					  MessageBox.Show("Locale was succesfully installed!", "Installing localization", MessageBoxButton.OK, MessageBoxImage.Information);
					  string name = System.IO.Path.GetFileName(files[i]);
					  sett.Program.Language = name.Substring(0, name.IndexOf(@".locale.xaml"));
					  SetLocale(sett.Program.Language);
				  }
				  else
					  MessageBox.Show("Can't install locale.", "Installing localization", MessageBoxButton.OK, MessageBoxImage.Error);
			  }
			  if (files[i].EndsWith(".theme.xaml"))
			  {
				  if (Slate.Themes.ThemesManager.InstallTheme(sett.Program.Path, files[i]))
				  {
					  MessageBox.Show("Theme was succesfully installed!", "Installing theme", MessageBoxButton.OK, MessageBoxImage.Information);
					  string name = System.IO.Path.GetFileName(files[i]);
					  sett.Program.Theme = name.Substring(0, name.IndexOf(@".theme.xaml"));
					  SetTheme(sett.Program.Theme);
				  }
				  else
					  MessageBox.Show("Can't install theme.", "Installing theme", MessageBoxButton.OK, MessageBoxImage.Error);
			  }
		  }
	}

	private void TileLibrary_Click(object sender, RoutedEventArgs e)
	{
		if (library != null && library.IsLoaded)
			library.Activate();
		else
		{
			library = new Library(this);
			library.Show();
		}
	}

	private void RemoveTilesItem_Click(object sender, RoutedEventArgs e)
	{
		for (int i = 0; i < TilesGrid.Children.Count; i++)
		{
			int index = Tiles.IndexOf((Tile)TilesGrid.Children[i]);
			Tiles[index].Unload();
			((MenuItem)AddTileItem.Items[index]).IsChecked = false;
		}
	}

	  public static int GetElementIndexByYCoord(StackPanel panel, double y)
	  {
		  Point pos;
		  for (int i = 0; i < panel.Children.Count; i++)
		  {
			  pos = panel.Children[i].PointToScreen(new Point(0, 0));
			  if (y > pos.Y && y < pos.Y + ((FrameworkElement)panel.Children[i]).Height)
				  return i;
		  }
		  if (y < panel.PointToScreen(new Point(0,0)).Y)
			  return -1;
		  else
			  return 100500;
	  }

	  private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
	  {
		  shadow.Height = this.Height;
		  shadow.Top = this.Top;
		  switch (sett.Program.Side)
		  {
			  case Slate.General.Sidebar.Side.Right:
				  shadow.Left = this.Left - shadow.Width;
				  break;
			  case Slate.General.Sidebar.Side.Left:
				  shadow.Left = this.Left + this.Width;
				  break;
		  }
	  }

	  public void EnableSnowFall()
	  {
		  if (SnowFallCanvas.Visibility == Visibility.Collapsed)
		  {
			  SnowFallCanvas.Visibility = Visibility.Visible;
			  SnowFallCanvas.Width = this.Width;
			  Random r = new Random(Environment.TickCount);
			  for (int i = 0; i < 50; i++)
			  {
				  SnowFall.SnowFlake snowFlake = new LongBar.SnowFall.SnowFlake();
				  snowFlake.SetValue(Canvas.LeftProperty, (double)r.Next((int)this.Width));
				  snowFlake.SetValue(Canvas.TopProperty, (double)r.Next((int)this.Height));
				  snowFlake.Width = 10 + r.Next(15);
				  snowFlake.Height = snowFlake.Width;
				  snowFlake.Visibility = Visibility.Visible;
				  SnowFallCanvas.Children.Add(snowFlake);
				  snowFlake.Enabled = true;
			  }
		  }
	  }

	  public void DisableSnowFall()
	  {
		  SnowFallCanvas.Visibility = Visibility.Collapsed;
		  foreach (LongBar.SnowFall.SnowFlake snowFlake in SnowFallCanvas.Children)
		  {
			  snowFlake.Enabled = false;
		  }
		  SnowFallCanvas.Children.Clear();
	  }

	  public static void ShowNotification()
	  {
		  Notify notify = new Notify();
		  notify.Left = System.Windows.Forms.SystemInformation.WorkingArea.Right - notify.Width;
		  notify.Top = System.Windows.Forms.SystemInformation.WorkingArea.Bottom - notify.Height;
		  notify.MouseLeftButtonDown += new MouseButtonEventHandler(notify_MouseLeftButtonDown);
		  notify.Show();
	  }

	  static void notify_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	  {
		  ((Window)sender).Close();
	  }
  }
}