import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  server: {
    // Dev proxy only: same-origin in production, served by the .NET host from src/web/dist.
    proxy: {
      "/api": "http://localhost:5000",
    },
  },
});
