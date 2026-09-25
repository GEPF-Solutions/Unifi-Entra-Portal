import { fileURLToPath, URL } from 'node:url';

import { defineConfig } from 'vite';
import plugin from '@vitejs/plugin-react';
import fs from 'fs';
import path from 'path';
import child_process from 'child_process';
import { env } from 'process';

const baseFolder =
    env.APPDATA !== undefined && env.APPDATA !== ''
        ? `${env.APPDATA}/ASP.NET/https`
        : `${env.HOME}/.aspnet/https`;

const certificateName = "unifi-entra-portal.client";
const certFilePath = path.join(baseFolder, `${certificateName}.pem`);
const keyFilePath = path.join(baseFolder, `${certificateName}.key`);

if (!fs.existsSync(baseFolder)) {
    fs.mkdirSync(baseFolder, { recursive: true });
}

if (!fs.existsSync(certFilePath) || !fs.existsSync(keyFilePath)) {
    if (0 !== child_process.spawnSync('dotnet', [
        'dev-certs',
        'https',
        '--export-path',
        certFilePath,
        '--format',
        'Pem',
        '--no-password',
    ], { stdio: 'inherit', }).status) {
        throw new Error("Could not create certificate.");
    }
}

const target = env.ASPNETCORE_HTTPS_PORT ? `https://localhost:${env.ASPNETCORE_HTTPS_PORT}` :
    env.ASPNETCORE_URLS ? env.ASPNETCORE_URLS.split(';')[0] : 'https://localhost:7260';

// https://vitejs.dev/config/
export default defineConfig({
    plugins: [plugin()],
    resolve: {
        alias: {
            '@': fileURLToPath(new URL('./src', import.meta.url))
        }
    },
    server: {
        proxy: {
            '^/api': {
                target,
                secure: false
            },
            // OpenID Connect callback paths that Microsoft's login page
            // posts/redirects to directly on this origin — these are handled
            // by ASP.NET Core's authentication middleware, not the SPA, so
            // they need to be proxied to the backend just like /api.
            '^/signin-oidc': {
                target,
                secure: false
            },
            '^/signout-oidc': {
                target,
                secure: false
            },
            '^/signout-callback-oidc': {
                target,
                secure: false
            },
            // Operator-supplied branding assets (logo, hero image) — served
            // by the backend from PortalBrandingSettings.AssetsPath, not
            // part of the SPA bundle. See PortalController.GetConfig /
            // Program.cs's "/branding" static files mount.
            '^/branding': {
                target,
                secure: false
            }
        },
        port: parseInt(env.DEV_SERVER_PORT || '51366'),
        https: {
            key: fs.readFileSync(keyFilePath),
            cert: fs.readFileSync(certFilePath),
        }
    }
})
