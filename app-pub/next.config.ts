import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  experimental: {
    cpus: 1,
    staticGenerationRetryCount: 3,
  },
};

export default nextConfig;
