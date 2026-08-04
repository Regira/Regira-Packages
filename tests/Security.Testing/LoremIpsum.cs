namespace Security.Testing;

public static class LoremIpsum
{
    // Explicit \r\n instead of a multi-line verbatim literal: git checks this file out with the
    // platform's line endings (CRLF on Windows, LF on the Linux CI runner), and the recorded
    // ciphertexts in Symmetric_Tests encode the CRLF text — so the constant must not follow the checkout.
    public const string Value =
        "Lorem ipsum dolor sit amet, consectetur adipiscing elit.\r\n" +
        "Praesent interdum cursus sapien, a rutrum sapien aliquam quis. Mauris mattis, justo non pulvinar semper, diam lorem tincidunt leo, id mollis ex lorem eu erat.\r\n" +
        "In hac habitasse platea dictumst. Cras ante est, pharetra ut erat vel, volutpat dapibus augue.\r\n" +
        "Maecenas viverra eros et gravida gravida. Nullam vel massa magna. Etiam ac nunc semper urna dignissim bibendum.\r\n" +
        "Nam in massa vel mi varius dignissim. Mauris dolor mi, tempus eget accumsan sed, porttitor consequat diam.";
}
