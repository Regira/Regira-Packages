namespace Regira.Security.Authentication.Core.Models;

/// <summary>
/// Marks a registered scheme as one that can <em>start an interactive sign-in</em> — i.e. answer a challenge by
/// sending the caller somewhere to authenticate, rather than by refusing the request.
/// <para>
/// The scheme selector needs this because its forwarding rules key on the credential a request <b>carries</b>, and a
/// browser arriving at a guarded page carries none. Without it the challenge falls through to the lowest-ordered
/// rule — a bearer <c>401</c> — and interactive sign-in is unreachable in an app whose whole point is signing in.
/// </para>
/// </summary>
/// <param name="AuthenticationScheme">The scheme to forward challenges to.</param>
public sealed record InteractiveSignInScheme(string AuthenticationScheme);
