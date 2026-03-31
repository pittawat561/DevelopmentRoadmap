import { z } from "zod";

export const loginSchema = z.object({
    email: z
        .string()
        .min(1, "Email is required.")
        .email("Invalid email format."),
    password: z
        .string()
        .min(1, "Password is required."),
});

export const registerSchema = z
    .object({
        firstName: z
            .string()
            .min(1, "First name is required.")
            .max(100),
        lastName: z
            .string()
            .min(1, "Last name is required.")
            .max(100),
        email: z
            .string()
            .min(1, "Email is required.")
            .email("Invalid email format.")
            .max(255),
        phone: z
            .string()
            .min(1, "Phone is required.")
            .regex(/^0\d{8,9}$/, "Invalid Thai phone number format."),
        password: z
            .string()
            .min(8, "Password must be at least 8 characters.")
            .regex(/[A-Z]/, "Must contain at least one uppercase letter.")
            .regex(/[a-z]/, "Must contain at least one lowercase letter.")
            .regex(/\d/, "Must contain at least one digit."),
        confirmPassword: z
            .string()
            .min(1, "Please confirm your password."),
    })
    .refine((data) => data.password === data.confirmPassword, {
        message: "Passwords do not match.",
        path: ["confirmPassword"],
    });

export type LoginFormValues = z.infer<typeof loginSchema>;
export type RegisterFormValues = z.infer<typeof registerSchema>;