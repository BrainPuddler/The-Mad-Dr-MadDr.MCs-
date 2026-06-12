/** Service errors carrying an HTTP status, mapped in http.ts. */
export class ServiceError extends Error {
  constructor(
    readonly status: number,
    readonly code: string,
    message: string,
  ) {
    super(message);
    this.name = "ServiceError";
  }
}

export const badRequest = (m: string) => new ServiceError(400, "bad_request", m);
export const unauthorized = (m: string) => new ServiceError(401, "unauthorized", m);
export const forbidden = (m: string) => new ServiceError(403, "forbidden", m);
export const notFound = (m: string) => new ServiceError(404, "not_found", m);
export const conflict = (m: string) => new ServiceError(409, "conflict", m);
export const paymentRequired = (m: string) => new ServiceError(402, "insufficient_components", m);
