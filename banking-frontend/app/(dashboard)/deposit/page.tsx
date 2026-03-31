"use client";

import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { depositSchema, type DepositFormValues } from "@/lib/validations/transaction";
import { useDeposit } from "@/lib/hooks/use-transactions";
import { useAccounts } from "@/lib/hooks/use-accounts";
import { useAuthStore } from "@/lib/stores/auth-store";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { CheckCircle } from "lucide-react";

export default function DepositPage() {
    const user = useAuthStore((state) => state.user);
    const { data: accounts } = useAccounts(user?.id || "");
    const deposit = useDeposit();

    const form = useForm<DepositFormValues>({
        resolver: zodResolver(depositSchema),
        defaultValues: { accountId: "", amount: 0, description: "" },
    });

    const onSubmit = (data: DepositFormValues) => {
        deposit.mutate(data, {
            onSuccess: () => {
                form.reset();
            },
        });
    };

    return (
        <div className="mx-auto max-w-lg p-6">
            <Card>
                <CardHeader>
                    <CardTitle>Deposit Money</CardTitle>
                    <CardDescription>Add funds to your account</CardDescription>
                </CardHeader>
                <CardContent>
                    {/* Success Message */}
                    {deposit.isSuccess && (
                        <Alert className="mb-4 border-green-200 bg-green-50">
                            <CheckCircle className="h-4 w-4 text-green-600" />
                            <AlertDescription className="text-green-800">
                                Deposit of{" "}
                                {formatCurrency(deposit.data?.data?.data?.amount || 0)}{" "}
                                completed successfully!
                            </AlertDescription>
                        </Alert>
                    )}

                    {/* Error Message */}
                    {deposit.isError && (
                        <Alert variant="destructive" className="mb-4">
                            <AlertDescription>
                                {(deposit.error as any)?.response?.data?.message ||
                                    "Deposit failed. Please try again."}
                            </AlertDescription>
                        </Alert>
                    )}

                    <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
                        {/* Account Select */}
                        <div className="space-y-2">
                            <Label>Account</Label>
                            <select
                                className="w-full rounded-md border p-2"
                                {...form.register("accountId")}
                            >
                                <option value="">Select an account</option>
                                {accounts?.map((acc) => (
                                    <option key={acc.id} value={acc.id}>
                                        {acc.accountNumber} ({acc.type}) —{" "}
                                        {formatCurrency(acc.balance)}
                                    </option>
                                ))}
                            </select>
                            {form.formState.errors.accountId && (
                                <p className="text-sm text-red-500">
                                    {form.formState.errors.accountId.message}
                                </p>
                            )}
                        </div>

                        {/* Amount */}
                        <div className="space-y-2">
                            <Label>Amount (THB)</Label>
                            <Input
                                type="number"
                                step="0.01"
                                placeholder="0.00"
                                {...form.register("amount", { valueAsNumber: true })}
                            />
                            {form.formState.errors.amount && (
                                <p className="text-sm text-red-500">
                                    {form.formState.errors.amount.message}
                                </p>
                            )}
                        </div>

                        {/* Description */}
                        <div className="space-y-2">
                            <Label>Description (Optional)</Label>
                            <Input
                                placeholder="e.g., Monthly savings"
                                {...form.register("description")}
                            />
                        </div>

                        <Button
                            type="submit"
                            className="w-full"
                            disabled={deposit.isPending}
                        >
                            {deposit.isPending ? "Processing..." : "Deposit"}
                        </Button>
                    </form>
                </CardContent>
            </Card>
        </div>
    );
}

function formatCurrency(amount: number): string {
    return new Intl.NumberFormat("th-TH", {
        style: "currency",
        currency: "THB",
    }).format(amount);
}