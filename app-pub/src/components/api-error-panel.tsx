import type { ApiError } from "@/lib/http";

type ApiErrorPanelProps = {
  error: ApiError;
  showFieldErrors?: boolean;
};

export function fieldErrorsFor(
  error: ApiError | null,
  key: string,
) {
  const errors = error?.problem.errors;

  if (!errors) return [];

  const matchingKey = Object.keys(errors).find(
    item => item.toLowerCase() === key.toLowerCase(),
  );

  return matchingKey ? errors[matchingKey] : [];
}

export function FieldErrors({ messages }: { messages: string[] }) {
  if (messages.length === 0) return null;

  return (
    <ul
      role="alert"
      className="mt-1 list-disc space-y-1 pl-5 text-sm text-red-700"
    >
      {messages.map((message, index) => (
        <li key={`${message}-${index}`}>{message}</li>
      ))}
    </ul>
  );
}

export default function ApiErrorPanel({
  error,
  showFieldErrors = true,
}: ApiErrorPanelProps) {
  const hasFieldErrors = Object.keys(error.problem.errors ?? {}).length > 0;

  return (
    <div
      role="alert"
      className="space-y-2 rounded-xl border border-red-200 bg-red-50 p-4 text-red-950"
    >
      <p className="font-medium">
        {hasFieldErrors ? "Verifică datele marcate și încearcă din nou." : error.message}
      </p>

      {showFieldErrors && error.problem.errors && (
        <ul className="list-disc space-y-1 pl-5 text-sm">
          {Object.entries(error.problem.errors).flatMap(([field, messages]) =>
            messages.map((message, index) => (
              <li key={`${field}-${index}`}>{message}</li>
            )),
          )}
        </ul>
      )}

      {error.problem.traceId && (
        <p className="break-all text-xs text-red-800">
          Cod de urmărire: {error.problem.traceId}
        </p>
      )}
    </div>
  );
}
