namespace WindowsCredentialProviderTest
{
    using CredentialProvider.Interop;
    using System.Runtime.InteropServices;

    [ComVisible(true)]
    [Guid("093755F9-E026-4F61-84A8-485665F57ED0")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ITestWindowsCredentialProvider : ICredentialProvider
    {
    }
}