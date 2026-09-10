import type { ApiError } from "@/lib/http";

export default function ApiErrorPanel({
  error,
  showFieldErrors = true,
}: {
  error: ApiError;
  showFieldErrors?: boolean;
}) {
  const hasFieldErrors = Object.keys(error.problem.errors ?? {}).length > 0;

  return (
    <div
      role="alert"
      className="space-y-2 rounded-lg border border-red-200 bg-red-50 p-4 text-red-900"
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
        <p className="break-all text-sm">Cod de urmărire: {error.problem.traceId}</p>
      )}
    </div>
  );
}
