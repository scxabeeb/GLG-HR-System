# Payroll approval workflow

HR (or Admin for preparation) generates and recalculates payroll. Admin/Finance reviews the calculation and submits it. The General Secretary gives final approval; Admin alone cannot perform that approval. Admin/Finance records payment only after approval. No roles are assigned automatically; an administrator grants Finance and General Secretary access in Users & permissions. Branch and module restrictions continue to apply.

Finance rejection returns generated payroll to HR preparation. General Secretary rejection returns submitted payroll to Admin/Finance review. A reason is required and the stage, actor and timestamp are retained in approval history. Recalculation and source-data changes invalidate pending approval.

Existing paid runs remain paid and immutable. Existing locked runs remain approved snapshots. Existing legacy reviewed runs must be submitted to the General Secretary before approval. Draft and calculated runs follow the new workflow on their next transition. No historical payroll amounts are recalculated by deployment.

Registered basic salary is not changed by work rules or payroll calculation. Salary-based attendance deductions use monthly basic ÷ the configured salary divisor ÷ required daily minutes × missing minutes; with 26 days and eight hours this is basic ÷ 26 ÷ 480 × missing minutes. Employment-date proration has been removed: payroll retains the full registered basic salary and allowances, including partial employment months. Attendance is only assessed on scheduled days within the employment dates. Attendance deductions are separate deductions from earnings, as is approved unpaid leave; approved paid leave has no attendance penalty. Legacy fixed-rate settings are retained for historical reference; new payroll uses the salary-based minute formula.

HR preparation applies attendance deductions. Attendance waivers and net-pay overrides require Admin/Finance; HR generation alone does not grant these financial adjustment permissions.

Salary days are stored on each recalculated run, defaulting to 26. New payroll calculations use salary-based minute deductions, with daily required hours from the effective work rule; legacy fixed-rate settings are retained only as historical reference. Old run metadata stays null until recalculation, preserving paid snapshots. Recalculate unapproved runs to adopt the new policy before submission.

Daily attendance entries carry rounding to keep their sum equal to the rounded monthly deduction. For example, 26 full absent days on a 400 salary deduct exactly 400, not 399.88 from independently rounding each day.
