import { z } from "zod";

export const depositSchema = z.object({
    accountId: z.string().min(1, "Account is required."),
    amount: z
        .number({ error: "Amount must be a number." })
        .positive("Amount must be greater than 0.")
        .max(1_000_000, "Amount cannot exceed 1,000,000 per transaction."),
    description: z.string().max(500).optional(),
});

export const withdrawSchema = z.object({
    accountId: z.string().min(1, "Account is required."),
    amount: z
        .number({ error: "Amount must be a number." })
        .positive("Amount must be greater than 0.")
        .max(1_000_000, "Amount cannot exceed 1,000,000 per transaction."),
    description: z.string().max(500).optional(),
});

export const transferSchema = z
    .object({
        fromAccountId: z.string().min(1, "Source account is required."),
        toAccountId: z.string().min(1, "Destination account is required."),
        amount: z
            .number({ error: "Amount must be a number." })
            .positive("Amount must be greater than 0.")
            .max(1_000_000, "Amount cannot exceed 1,000,000 per transaction."),
        description: z.string().max(500).optional(),
    })
    .refine((data) => data.fromAccountId !== data.toAccountId, {
        message: "Cannot transfer to the same account.",
        path: ["toAccountId"],
    });

export type DepositFormValues = z.infer<typeof depositSchema>;
export type WithdrawFormValues = z.infer<typeof withdrawSchema>;
export type TransferFormValues = z.infer<typeof transferSchema>;
