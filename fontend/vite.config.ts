import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";
import path from "node:path";

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "./src"),
    },
  },
  server: {
    port: 5174,
    // Mặc định Vite bind "localhost" → trên Windows/Node 18+ chỉ ra IPv6 [::1]. ngrok (và mọi client
    // nối 127.0.0.1) sẽ báo ERR_NGROK_8012 dù server đang chạy. Ép IPv4 để tunnel hoạt động.
    host: "127.0.0.1",
    allowedHosts: true,
  },
  test: {
    environment: "jsdom",
    setupFiles: ["./src/test/setup.ts"],
  },
});
