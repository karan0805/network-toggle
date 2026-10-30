---
name: Bug report
about: Something isn't working as expected
labels: bug
---

**What happened?**

**What did you expect to happen?**

**Steps to reproduce**

1.
2.
3.

**Your setup**

- Network Toggle version:
- Windows version:
- Connections in use (Ethernet, Wi-Fi, USB tethering):

**Adapter details**

If a connection is missing or has the wrong label, run this in PowerShell and paste the result:

```powershell
Get-NetAdapter | Select-Object Name, InterfaceDescription, Status
```
