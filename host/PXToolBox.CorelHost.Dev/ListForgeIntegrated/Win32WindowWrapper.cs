using System;

namespace PXToolBoxDev.ListForgeIntegrated
{
    /// <summary>Adapta um HWND para <see cref="System.Windows.Forms.IWin32Window"/> — permite
    /// passar uma janela WPF como "owner" de um diálogo nativo do WinForms (ex.:
    /// <see cref="System.Windows.Forms.FolderBrowserDialog"/>), usado por
    /// <see cref="AdvancedSaveDialog"/> e <see cref="SettingsWindow"/>.</summary>
    internal sealed class Win32WindowWrapper : System.Windows.Forms.IWin32Window
    {
        public Win32WindowWrapper(IntPtr handle) => Handle = handle;
        public IntPtr Handle { get; }
    }
}
