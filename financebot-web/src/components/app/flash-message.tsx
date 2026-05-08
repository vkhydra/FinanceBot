import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";

type FlashMessageProps = {
  title: string;
  message: string;
  variant?: "default" | "destructive";
};

export function FlashMessage({
  title,
  message,
  variant = "default",
}: FlashMessageProps) {
  return (
    <Alert variant={variant}>
      <AlertTitle>{title}</AlertTitle>
      <AlertDescription>{message}</AlertDescription>
    </Alert>
  );
}
