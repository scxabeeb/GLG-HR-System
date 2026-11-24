# Employee registration and sign-in

New employees receive IDs automatically: EMP0001, EMP0002, and onward (at least four digits). A database counter is locked within the registration transaction; existing employee numbers and usernames are skipped. Existing employees are not renumbered. Failed registrations roll back the counter along with the employee and login.

Registration creates a linked Employee account whose username is the generated ID. Its initial password is the ID followed by 123, for example EMP0001123. Both web and mobile require a password change before the employee can use protected features. Only generated registration credentials use this format; password changes and recovery retain the regular password policy. The credentials appear once on the onboarding confirmation and passwords are never exported.

Employee imports use the same numbering and login workflow. Number and InitialPassword columns remain in CSV templates for compatibility but their values are ignored on new registration. Email remains required for account recovery.
