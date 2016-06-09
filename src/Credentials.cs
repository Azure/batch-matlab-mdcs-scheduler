// Copyright (c) Microsoft Corporation

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MatlabBatchLib
{
    /// <summary>
    /// CRED_TYPE enum from wincred.h
    /// </summary>
    internal enum CRED_TYPE : int
    {
        GENERIC = 1,
        DOMAIN_PASSWORD = 2,
        DOMAIN_CERTIFICATE = 3,
        DOMAIN_VISIBLE_PASSWORD = 4,
        MAXIMUM = 5
    }

    /// <summary>
    /// CRED_PERSIST enum from wincred.h
    /// </summary>
    internal enum CRED_PERSIST : int
    {
        NONE = 0,
        SESSION = 1,
        LOCAL_MACHINE = 2,
        ENTERPRISE = 3
    }

    /// <summary>
    /// CREDENTIAL struct from wincred.h
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct CREDENTIAL
    {
        public int Flags;
        public int Type;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string TargetName;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public int CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist;
        public int AttributeCount;
        public IntPtr Attributes;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string TargetAlias;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string UserName;
    }

    /// <summary>
    /// Class used for PInvoke of Windows credential management APIs.
    /// </summary>
    internal class Credentials
    {
        [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredRead(string target, CRED_TYPE type, int flags, out IntPtr credential);

        [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredWrite(ref CREDENTIAL credential, int flags);

        /// <summary>
        /// Gets the stored key for the specified target.
        /// </summary>
        /// <param name="target">The target to get the key for.</param>
        /// <returns>The stored key for the specified target.</returns>
        internal static string GetStoredKey(string target)
        {
            CREDENTIAL cred;
            IntPtr credPtr;

            if (!CredRead(target, CRED_TYPE.GENERIC, 0, out credPtr))
            {
                int lastError = Marshal.GetLastWin32Error();
                throw new InvalidOperationException(string.Format("Failed to retrieve credential for {0}. Last error: {1}", target, lastError));
            }

            cred = (CREDENTIAL)Marshal.PtrToStructure(credPtr, typeof(CREDENTIAL));
            string key = Marshal.PtrToStringUni(cred.CredentialBlob, cred.CredentialBlobSize / 2); // Divide by 2 since we're using Unicode chars
            
            return key;
        }

        /// <summary>
        /// Stores a credential in the Windows Credential Manager on the local machine.
        /// </summary>
        /// <param name="target">The target name of the credential.</param>
        /// <param name="key">The credential key.</param>
        internal static void StoreCredential(string target, string key)
        {
            CREDENTIAL cred = new CREDENTIAL();
            cred.Type = (int)CRED_TYPE.GENERIC;
            cred.Persist = (int)CRED_PERSIST.LOCAL_MACHINE;

            cred.TargetName = target;
            cred.UserName = target;
            cred.CredentialBlob = Marshal.StringToHGlobalUni(key);
            cred.CredentialBlobSize = key.Length * 2; // Multiply by 2 since we're using Unicode chars

            if (!CredWrite(ref cred, 0))
            {
                int lastError = Marshal.GetLastWin32Error();
                throw new InvalidOperationException(string.Format("Failed to store credential for {0}. Last error: {1}", target, lastError));
            }
        }
    }
}
