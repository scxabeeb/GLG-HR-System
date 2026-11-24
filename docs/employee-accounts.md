# Employee registration and sign-in

New employees receive IDs automatically: EMP0001, EMP0002, and onward (at least four digits). A database counter is locked within the registration transaction; existing employee numbers and usernames are skipped. Existing employees are not renumbered. Failed registrations roll back the counter along with the employee and login.

Registration creates a linked Employee account whose username is the generated ID. Its initial password is the ID followed by 123, for example EMP0001123. Both web and mobile require a password change before the employee can use protected features. Only generated registration credentials use this format; password changes and recovery retain the regular password policy. Registration and onboarding do not display login details. Passwords are never exported.

Employee imports use the same numbering and login workflow. Number, Pin and InitialPassword columns remain in CSV templates for compatibility but their values are ignored on new registration. Email remains required for account recovery.

A unique numeric ZKTeco PIN is also allocated automatically under the registration transaction lock. Allocation skips existing employee PINs, received punch PINs, clock-review records and queued terminal commands. Selecting a paired terminal queues the employee profile automatically. Existing employee PINs are preserved.
