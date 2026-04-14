namespace AMN.ManifestGen
{
    internal class DllManifest
    {
        public string AssemblyName { get; set; }

        public string Version { get; set; }

        public string PublicToken { get; set; }

        public string FullPath { get; set; }

        public override string ToString()
        {
            return AssemblyName;
        }

        public override bool Equals(object obj)
        {
            if (obj is not DllManifest manifest) { return false; }
            return FullPath.Equals(manifest.FullPath);
        }
    }
}
