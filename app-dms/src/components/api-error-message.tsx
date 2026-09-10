import ApiErrorPanel from "@/components/api-error-panel";
import type { ApiError } from "@/lib/http";

export default function ApiErrorMessage({
  error,
}: {
  error: ApiError;
}) {
  return <ApiErrorPanel error={error} />;
}
