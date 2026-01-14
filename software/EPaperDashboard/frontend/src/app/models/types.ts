export interface User {
  id: string;
  username: string;
  isSuperUser: boolean;
}

export interface LoginRequest {
  username: string;
  password: string;
}

export interface RegisterRequest {
  username: string;
  password: string;
}

export interface Dashboard {
  id: string;
  userId: string;
  name: string;
  description: string;
  apiKey: string;
  accessToken?: string;
  host?: string;
  path?: string;
  updateTimes?: string[];
}

export interface CreateDashboardRequest {
  name: string;
  description?: string;
}

export interface UpdateDashboardRequest {
  name?: string;
  description?: string;
  accessToken?: string;
  host?: string;
  path?: string;
  updateTimes?: string[];
}
