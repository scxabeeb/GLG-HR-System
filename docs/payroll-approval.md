# Payroll approval workflow

HR (or Admin for preparation) generates and recalculates payroll. Admin/Finance reviews the calculation and submits it. The General Secretary gives final approval; Admin alone cannot perform that approval. Admin/Finance records payment only after approval. No roles are assigned automatically; an administrator grants Finance and General Secretary access in Users & permissions. Branch and module restrictions continue to apply.

Finance rejection returns generated payroll to HR preparation. General Secretary rejection returns submitted payroll to Admin/Finance review. A reason is required and the stage, actor and timestamp are retained in approval history. Recalculation and source-data changes invalidate pending approval.

Existing paid runs remain paid and immutable. Existing locked runs remain approved snapshots. Existing legacy reviewed runs must be submitted to the General Secretary before approval. Draft and calculated runs follow the new workflow on their next transition. No historical payroll amounts are recalculated by deployment.

Registered basic salary is not changed by work rules or payroll calculation. Salary-based attendance deductions use monthly basic ÷ the configured salary divisor ÷ required daily minutes × missing minutes; with 30 days and eight hours this is basic ÷ 30 ÷ 480 × missing minutes. Employment-date proration continues to determine basic pay for a partial employment period. Attendance deductions are separate deductions from earnings, as is approved unpaid leave; approved paid leave has no attendance penalty. Existing fixed-rate settings remain configurable.
