import { httpRequest } from '../../../shared/api/httpClient';

export interface PricingRule {
  id: number;
  roomTypeId: number;
  roomTypeName: string;
  date: string;
  price: number;
  reason: string;
}

export interface PricingRuleInput {
  roomTypeId: number;
  date: string;
  price: number;
  reason: string;
}

export interface PricingRuleFilters {
  from?: string;
  to?: string;
  roomTypeId?: number;
}

export async function listPricingRules(accessToken: string, filters?: PricingRuleFilters): Promise<PricingRule[]> {
  const query = new URLSearchParams();

  if (filters?.from) {
    query.set('from', filters.from);
  }

  if (filters?.to) {
    query.set('to', filters.to);
  }

  if (filters?.roomTypeId) {
    query.set('roomTypeId', String(filters.roomTypeId));
  }

  const queryString = query.toString();
  const path = queryString ? `/api/pricing-rules?${queryString}` : '/api/pricing-rules';

  return httpRequest<PricingRule[]>(path, { accessToken });
}

export async function createPricingRule(payload: PricingRuleInput, accessToken: string): Promise<PricingRule> {
  return httpRequest<PricingRule>('/api/pricing-rules', {
    method: 'POST',
    accessToken,
    body: payload,
  });
}

export async function updatePricingRule(id: number, payload: PricingRuleInput, accessToken: string): Promise<PricingRule> {
  return httpRequest<PricingRule>(`/api/pricing-rules/${id}`, {
    method: 'PUT',
    accessToken,
    body: payload,
  });
}

export async function deletePricingRule(id: number, accessToken: string): Promise<void> {
  await httpRequest<void>(`/api/pricing-rules/${id}`, {
    method: 'DELETE',
    accessToken,
  });
}
