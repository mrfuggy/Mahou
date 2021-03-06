﻿using System;
using System.Diagnostics;
using System.Threading;
using System.IO;
using System.Runtime.InteropServices;

namespace Mahou {
	static class jklXHidServ {
		public static uint cycleEmuDesiredLayout = 0;
		public static bool start_cyclEmuSwitch = false;
		public static Action ActionOnLayout;
		public static uint OnLayoutAction = 0;
		public static int jkluMSG = -1;
		public static bool running = false, self_change, actionOnLayoutExecuted;
		/// <summary>0=exe, 1=dll, 2=x86.exe, 3=x86.dll</summary>
		public static bool[] jklFEX = new bool[5];
		public static string jklInfoStr = "";
		static IntPtr HWND = IntPtr.Zero;
		static WinAPI.WndProc WNDPROC_DELEGATE;
	    static public void Destroy() {
			if (HWND != IntPtr.Zero) {
				var serv = WinAPI.FindWindow("_HIDDEN_HWND_SERVER", "_HIDDEN_HWND_SERVER");
				var x86help = WinAPI.FindWindow("_HIDDEN_X86_HELPER", "_HIDDEN_X86_HELPER");
				if (serv != IntPtr.Zero)
					WinAPI.PostMessage(serv, WinAPI.WM_QUIT, 0, 0);
				if (x86help != IntPtr.Zero)
					WinAPI.PostMessage(x86help, WinAPI.WM_QUIT, 0, 0);
				running = false;
				// Multiple CreateWindowEx & WM_DESTROY causes NullReference exception in NATIVE CODE!!
				// So its disabled for now... Create window 1 time and not destroy it.
//				WinAPI.PostMessage(HWND, WinAPI.WM_DESTROY, 0, 0); 
//		        HWND = IntPtr.Zero;
			}
	    }
		public static bool jklExist() {
			var pth = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "jkl");
			jklFEX[0] = File.Exists(pth+".exe");
			jklFEX[1] = File.Exists(pth+".dll");
			jklFEX[2] = File.Exists(pth+"x86.exe");
			jklFEX[3] = File.Exists(pth+"x86.dll");
			if (!Environment.Is64BitOperatingSystem) {
				if (jklFEX[2] && jklFEX[3])
					return true;
			}
			if (jklFEX[0] && jklFEX[1] && jklFEX[2] && jklFEX[3])
				return true;
			jklInfoStr = "jkl.exe " + (jklFEX[0] ? "" : MMain.Lang[Languages.Element.Not] + " ") + MMain.Lang[Languages.Element.Exist] + "\r\n";
			jklInfoStr += "jkl.dll " + (jklFEX[1] ? "" : MMain.Lang[Languages.Element.Not] + " ") + MMain.Lang[Languages.Element.Exist] + "\r\n";
			jklInfoStr += "jklx86.exe " + (jklFEX[2] ? "" : MMain.Lang[Languages.Element.Not] + " ") + MMain.Lang[Languages.Element.Exist] + "\r\n";
			jklInfoStr += "jklx86.dll " + (jklFEX[3] ? "" : MMain.Lang[Languages.Element.Not] + " ") + MMain.Lang[Languages.Element.Exist] + "\r\n";
			return false;
		}
	    public static void Init() {
			if (!MahouUI.ENABLED) return;
			if (running) {
				bool exist = true;
				if (Environment.Is64BitOperatingSystem) {
					if (Process.GetProcessesByName("jkl").Length > 0) {
						Logging.Log("[JKL] > JKL already running.");
					} else 
						exist = false;
				} else 	{
					if (Process.GetProcessesByName("jklx86").Length > 0) {
						Logging.Log("[JKL] > JKLx86 already running.");
					} else 
						exist = false;
				}
				if (!exist) {
					Logging.Log("[JKL] > JKL seems closed, restarting...");
					running = false;
				}
			}
			if (HWND == IntPtr.Zero) {
				Logging.Log("[JKL] > Initializing JKL HWND server...");
		        WNDPROC_DELEGATE = jklWndProc;
		        var wnd_class = new WinAPI.WNDCLASS();
		        wnd_class.lpszClassName = "_XHIDDEN_HWND_SERVER";
		        wnd_class.lpfnWndProc = Marshal.GetFunctionPointerForDelegate(WNDPROC_DELEGATE);
		        UInt16 cls_reg = WinAPI.RegisterClassW(ref wnd_class);
		        int last_error = Marshal.GetLastWin32Error();
		        if (cls_reg == 0 && last_error != 0) {
		            Logging.Log("[JKL] > Could not register window class, for jkl Hidden Server, err: " + last_error, 1);
		        }
		        HWND = WinAPI.CreateWindowExW(0, "_XHIDDEN_HWND_SERVER", "_XHIDDEN_HWND_SERVER", 0, 0, 0, 0, 0,
		                                      IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
		        Logging.Log("[JKL] > SERVER HWND: " +HWND);
			}
			if (!running) {
				if (jklExist()) {
					if (Environment.Is64BitOperatingSystem) {
						Logging.Log("[JKL] > Starting jkl.exe...");
						var jkl = new ProcessStartInfo();
						jkl.UseShellExecute = true;
						jkl.FileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "jkl.exe");
						jkl.WorkingDirectory = Path.Combine(Path.GetTempPath());
			        	Process.Start(jkl);
					} else {
						Logging.Log("[JKL] > Starting \"jklx86.exe -msg\"...");
						var jkl = new ProcessStartInfo();
						jkl.UseShellExecute = true;
						jkl.FileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "jklx86.exe");
						jkl.Arguments = "-msg";
						jkl.WorkingDirectory = Path.Combine(Path.GetTempPath());
			        	Process.Start(jkl);
					}
					var umsgID = Path.Combine(Path.GetTempPath(), "umsg.id");
					var tries = 0;
					while (!File.Exists(umsgID)) {
						Thread.Sleep(350);
						tries++;
						if (tries > 20) {
							Logging.Log("[JKL] > Error, umsg.id not found after 20 tries by 350 ms timeout.", 1);
							Destroy();
							break;
						}
					}
					if (tries <= 20) {
						Logging.Log("[JKL] > umsg.id found, after " + tries + " tries * 350ms timeout.");
						Logging.Log("[JKL] > Retrieving umsg.id...");
						jkluMSG = Int32.Parse(File.ReadAllText(umsgID));
						File.Delete(umsgID);
//						KMHook.DoLater(() => CycleAllLayouts(Locales.ActiveWindow()), 350);
						KMHook.DoLater(() => { MahouUI.GlobalLayout = MahouUI.currentLayout = Locales.GetCurrentLocale(); }, 200);
						running = true;
					}
				} else {
					Logging.Log("[JKL] > " + jklInfoStr, 1);
				}
				if (jkluMSG == -1)
					KMHook.JKLERR = true;
				else 
					KMHook.JKLERR = false;
				Logging.Log("[JKL] > Init done, umsg: ["+jkluMSG+"], JKLXServ: ["+HWND+"].");
			}
	    }
		public static void CycleAllLayouts(IntPtr hwnd) {
			self_change = true;
//			for (int i = 0; i!=MMain.PHLayouts; i++) {
//				if (MMain.MahouActive()) return; // Else creates invalid culture 0 exception.
				WinAPI.SendMessage(hwnd, (int)WinAPI.WM_INPUTLANGCHANGEREQUEST, 0, WinAPI.HKL_NEXT);
//				Logging.Log("[JKL] > Cycle all: "+i+"/"+MMain.PHLayouts);
//				Thread.Sleep(5);
				WinAPI.SendMessage(hwnd, (int)WinAPI.WM_INPUTLANGCHANGEREQUEST, 0, WinAPI.HKL_PREV);
//			}/
			self_change = false;
		}
	    static IntPtr jklWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)  {
			if (msg == jkluMSG) {
				uint layout = (uint)lParam, laysho = layout & 0xffff;
				MahouUI.GlobalLayout = MahouUI.currentLayout = layout;
				Logging.Log("[JKL] > Layout changed to [" + layout + "] / [0x"+layout.ToString("X") +"].");
				Debug.WriteLine(">> JKL LC: " + layout);
				Logging.Log("[JKL] > On layout act:" +OnLayoutAction);
				var anull = ActionOnLayout==null;
				Logging.Log("[JKL] > ACtion: " +(anull?"null":ActionOnLayout.Method.Name));
				if (layout == OnLayoutAction || (layout & 0xffff) == (OnLayoutAction & 0xffff)) {
					actionOnLayoutExecuted = true;
					OnLayoutAction = 0;
					if (anull) {
						Logging.Log("[JKL] > Action is null, something terribly went wrong... Please try to disable JKL, if layout changing went wild.",1);
					} else {
						Debug.WriteLine("Executing action: " + ActionOnLayout.Method.Name + " on layout: " +layout);
					    ActionOnLayout();
					    ActionOnLayout = null;
					}
				}
				if (start_cyclEmuSwitch) {
					Debug.WriteLine("Cycling out from: "+ layout + " to " + cycleEmuDesiredLayout +"...");
					if (layout != cycleEmuDesiredLayout && laysho != cycleEmuDesiredLayout)
						KMHook.CycleEmulateLayoutSwitch();
					else
						start_cyclEmuSwitch = false;
				}
				if (!self_change && !start_cyclEmuSwitch) {
					MahouUI.RefreshFLAG();
					MMain.mahou.RefreshAllIcons();
					MMain.mahou.UpdateLDs();
					if (anull && !KMHook.selfie && KMHook.AS_IGN_LS) {
						if (KMHook.AS_IGN_RULES.Contains("L")) {
							Debug.WriteLine("[HEY] > "+ KMHook.was_ls);
							if (KMHook.was_ls) {
								KMHook.was_ls = KMHook.was_back = KMHook.was_del = false;
							} else {
								KMHook.was_ls = true;
								if(KMHook.AS_IGN_RULES.Contains("T")) {
									if (KMHook.AS_IGN_RESET != null) {
										KMHook.AS_IGN_RESET.Stop();
										KMHook.AS_IGN_RESET.Dispose();
									}
									KMHook.AS_IGN_RESET = new System.Windows.Forms.Timer();
									KMHook.AS_IGN_RESET.Interval = KMHook.AS_IGN_TIMEOUT;
									KMHook.AS_IGN_RESET.Tick += (_,__) => {
										Debug.WriteLine("TIMER_AS_RESET");
										KMHook.was_ls = KMHook.was_back = KMHook.was_del = false; 
										KMHook.AS_IGN_RESET.Stop();
										KMHook.AS_IGN_RESET.Dispose();
									};
									KMHook.AS_IGN_RESET.Start();
								}
							}
						} else 
							KMHook.was_ls = true;
					}
				}
			}
	        return WinAPI.DefWindowProcW(hWnd, msg, wParam, lParam);
	    }
	}
}