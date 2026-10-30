# Security Policy

## Reporting a vulnerability

Please don't report security problems in a public issue. Use **Report a vulnerability** on the repository's **Security** tab so the details stay private.

Include what you found, how to reproduce it, and which version you tested.

## Supported versions

Only the latest release receives security fixes.

## What the app can access

Network Toggle runs with administrator rights because Windows requires them to change network priorities. It only reads adapter details and changes interface metrics. It doesn't collect data or send anything over the internet.

To find the connection in use, it asks Windows which route would reach `1.1.1.1`. This doesn't send any traffic to that address.
