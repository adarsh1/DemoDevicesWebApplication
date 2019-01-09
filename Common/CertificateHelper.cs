namespace Common
{
    using System;
    using System.Collections.Concurrent;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;

    public static class CertificateHelper
    {
        const StoreLocation DefaultStoreLocation = StoreLocation.LocalMachine;
        const StoreName DefaultStoreName = StoreName.My;
        const string CertificateSubjectPrifix = "CN=";

        static readonly ConcurrentDictionary<string, X509Certificate2> certificatesFromFiles = new ConcurrentDictionary<string, X509Certificate2>();

        public static X509Certificate2 GetCertificateFromFile(string fileName, string password)
        {
            X509Certificate2 certificate;
            if (certificatesFromFiles.TryGetValue(fileName, out certificate))
            {
                return certificate;
            }

            certificate = new X509Certificate2(fileName, password, X509KeyStorageFlags.Exportable);
            certificatesFromFiles[fileName] = certificate;
            return certificate;
        }

        public static X509Certificate2 GetCertificateFromPFX(byte[] pfxBytes, string password)
        {
            return new X509Certificate2(pfxBytes, password, X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
        }

        public static X509Certificate2 GetCertificateFromThumbprint(string thumbprint)
        {
            return GetCertificateFromThumbprint(thumbprint, StoreName.My, StoreLocation.LocalMachine);
        }

        public static X509Certificate2 GetCertificateFromThumbprint(string thumbprint, bool validOnly)
        {
            return GetCertificateFromThumbprint(thumbprint, StoreName.My, StoreLocation.LocalMachine, validOnly);
        }

        public static X509Certificate2 GetCertificateFromThumbprint(string thumbprint, StoreName storeName, StoreLocation storeLocation)
        {
            return GetCertificateFromThumbprint(thumbprint, storeName, storeLocation, true);
        }

        public static X509Certificate2 GetCertificateFromThumbprint(string thumbprint, StoreName storeName, StoreLocation storeLocation, bool validOnly)
        {
            X509Store store = null;
            X509Certificate2 cert = null;

            try
            {
                store = new X509Store(storeName, storeLocation);
                store.Open(OpenFlags.ReadOnly);
                X509Certificate2Collection certCollection = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, validOnly);
                X509Certificate2Enumerator enumerator = certCollection.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    cert = enumerator.Current;
                }
            }
            finally
            {
                if (store != null)
                {
                    store.Close();
                }
            }

            return cert;
        }

        public static X509Certificate2 GetCertificateFromSubjectName(string subjectName)
        {
            return GetCertificateFromSubjectName(subjectName, StoreName.My, StoreLocation.LocalMachine);
        }

        public static X509Certificate2 GetCertificateFromSubjectName(string subjectName, bool validOnly)
        {
            return GetCertificateFromSubjectName(subjectName, StoreName.My, StoreLocation.LocalMachine, validOnly);
        }

        public static X509Certificate2 GetCertificateFromSubjectName(string subjectName, StoreName storeName, StoreLocation storeLocation)
        {
            return GetCertificateFromSubjectName(subjectName, storeName, storeLocation, true);
        }

        public static string StripCertificateSubject(string certSubject, string strip = CertificateSubjectPrifix)
        {
            if (null != certSubject && certSubject.StartsWith(strip, StringComparison.OrdinalIgnoreCase))
            {
                // strip off the CN= start 
                certSubject = certSubject.Substring(strip.Length);
            }
            return certSubject;
        }

        public static X509Certificate2 GetCertificateFromSubjectName(string subjectName, StoreName storeName, StoreLocation storeLocation, bool validOnly)
        {
            X509Store store = null;
            X509Certificate2 cert = null;
            
            try
            {
                store = new X509Store(storeName, storeLocation);
                store.Open(OpenFlags.ReadOnly);

                subjectName = StripCertificateSubject(subjectName);
                
                X509Certificate2Collection certCollection = store.Certificates.Find(X509FindType.FindBySubjectName, subjectName, validOnly);
                X509Certificate2Enumerator enumerator = certCollection.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    if (null == cert)
                    {
                        cert = enumerator.Current;
                    }
                    else
                    {
                        if (enumerator.Current.NotAfter > cert.NotAfter)
                            cert = enumerator.Current;
                    }
                }
            }
            finally
            {
                if (store != null)
                {
                    store.Close();
                }
            }

            return cert;
        }

        public static byte[] GetPFXFromCertificate(X509Certificate2 certificate, string password)
        {
            if (!certificate.HasPrivateKey)
            {
                throw new InvalidOperationException("Supplied certificate does not have an exportable private key");
            }

            return certificate.Export(X509ContentType.Pfx, password);
        }

        public static void InstallCertificate(X509Certificate2 certificate)
        {
            InstallCertificate(certificate, StoreLocation.LocalMachine);
        }

        public static void InstallCertificate(X509Certificate2 certificate, StoreLocation storeLocation)
        {
            var store = new X509Store(StoreName.My, storeLocation);

            try
            {
                store.Open(OpenFlags.ReadWrite);
                store.Add(certificate);
            }
            finally
            {
                store.Close();
            }
        }

        public static X509Certificate2 InstallCertificateFromFile(string pfxFile, string passwordFile)
        {
            return InstallCertificateFromFile(pfxFile, passwordFile, StoreLocation.LocalMachine);
        }

        public static X509Certificate2 InstallCertificateFromFile(string pfxFile, string passwordFile, StoreLocation storeLocation)
        {
            var certPassword = File.ReadAllText(passwordFile);
            X509Certificate2 certificate = CertificateHelper.GetCertificateFromFile(pfxFile, certPassword);
            CertificateHelper.InstallCertificate(certificate, storeLocation);

            return certificate;
        }

        public static string GetCommonNameFromSubject(string subject)
        {
            string commonName = null;
            string[] parts = subject.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in parts)
            {
                var partTrimed = part.Trim();
                if (partTrimed.StartsWith("CN", StringComparison.OrdinalIgnoreCase))
                {
                    string[] cnParts = partTrimed.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
                    if (cnParts.Length > 1)
                    {
                        commonName = cnParts[1].Trim();
                    }
                }
            }

            return commonName;
        }

        public static X509Certificate2 GetX509Certificate2(string certificateSecret)
        {
            X509Certificate2Collection collection = new X509Certificate2Collection();
            collection.Import(Convert.FromBase64String(certificateSecret));
            var cert = collection.Cast<X509Certificate2>().Single(s => s.HasPrivateKey);
            return cert;
        }

        public static async Task<X509Certificate2> GetCertificateFromLocalStoreAsync(string thumbprint)
        {
            X509Store store = new X509Store(DefaultStoreName, DefaultStoreLocation);
            try
            {
                store.Open(OpenFlags.ReadWrite);

                X509Certificate2 certificate = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false)[0];
                if (certificate == null)
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Certificate with thumbprint {0} not found in storelocation: {1} and storename: {2}", thumbprint, DefaultStoreLocation, DefaultStoreName));
                }

                return await Task.FromResult(certificate);
            }
            finally
            {
                store.Close();
            }
        }

        public static async Task<X509Certificate2> GetCertificateFromLocalStoreAsyncBySubjectName(string certificateSubjectName)
        {
            X509Store store = new X509Store(DefaultStoreName, DefaultStoreLocation);
            try
            {
                store.Open(OpenFlags.ReadWrite);

                certificateSubjectName = StripCertificateSubject(certificateSubjectName);
                X509Certificate2 certificate = null;

                X509Certificate2Collection certCollection = store.Certificates.Find(X509FindType.FindBySubjectName, certificateSubjectName, false);
                X509Certificate2Enumerator enumerator = certCollection.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    if (null == certificate)
                    {
                        certificate = enumerator.Current;

                    }
                    else
                    {
                        if (enumerator.Current.NotAfter > certificate.NotAfter)
                        {
                            certificate = enumerator.Current;
                        }

                    }
                }

                if (certificate == null)
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Certificate with subject {0} not found in storelocation: {1} and storename: {2}", certificateSubjectName, DefaultStoreLocation, DefaultStoreName));
                }

                return await Task.FromResult(certificate);
            }
            finally
            {
                store.Close();
            }
        }

        public static byte[] GetProtectedCertBytes(byte[] importCertBytes, string certPwd)
        {
            var certificateCollection = new X509Certificate2Collection();
            certificateCollection.Import(importCertBytes, certPwd, X509KeyStorageFlags.Exportable);
            byte[] protectedCertBytes = certificateCollection.Export(X509ContentType.Pfx, certPwd);
            return protectedCertBytes;
        }
    }
}
