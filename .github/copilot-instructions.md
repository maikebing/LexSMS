# Copilot Instructions

## 项目指南
- The user clarified that file downloads should target the local PC disk, not the modem file system, and they want detailed HTTP flow debug logging showing each AT command response.
- When adding tests, the user expects tests to exercise the actual production parsing logic, not duplicated helper logic in test code.
- In call hang-up implementations, prioritize using `AT+CHUP`, and fall back to `ATH` only if the former fails.