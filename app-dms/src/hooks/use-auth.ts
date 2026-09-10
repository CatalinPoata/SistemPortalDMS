"use client";

import { useEffect, useSyncExternalStore } from "react";

import {
  getAuthSnapshot,
  getServerAuthSnapshot,
  initializeAuth,
  subscribeAuth,
} from "@/lib/auth-store";

export function useAuth() {
  const auth = useSyncExternalStore(
    subscribeAuth,
    getAuthSnapshot,
    getServerAuthSnapshot,
  );

  useEffect(() => {
    initializeAuth();
  }, []);

  return auth;
}
