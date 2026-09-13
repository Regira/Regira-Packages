using NUnit.Framework;

// Deliberately serial, and staying that way. LicensingTests and LicenseStatusTests both write the
// process-wide LicenseValidator.TestPublicKey, and LicensingTests swaps Console.Out/Console.Error to capture
// output. Marking both [NonParallelizable] would leave almost nothing to run in parallel across four files,
// and the alternative — reshaping a production static to suit the tests — is not worth it here.
