namespace CallFree.AI.Security
{
    public static class SecretMasker
    {
        public static string Mask(string secret)
        {
            if (string.IsNullOrWhiteSpace(secret))
            {
                return "(empty)";
            }

            string trimmed = secret.Trim();
            if (trimmed.Length <= 8)
            {
                return new string('*', trimmed.Length);
            }

            return trimmed.Substring(0, 4) + "..." + trimmed.Substring(trimmed.Length - 4, 4);
        }
    }
}
