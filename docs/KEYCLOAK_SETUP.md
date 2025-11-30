# Keycloak Configuration Guide for Squirrel Wiki OIDC Plugin

This guide provides step-by-step instructions for configuring Keycloak to work with the Squirrel Wiki OIDC authentication plugin.

## Prerequisites

- A running Keycloak instance (version 20.0 or later recommended)
- Administrative access to Keycloak
- Squirrel Wiki instance with the OIDC plugin installed

## Step 1: Create a Realm (Optional)

If you want to use a dedicated realm for Squirrel Wiki:

1. Log in to the Keycloak Admin Console
2. Click on the realm dropdown in the top-left corner
3. Click **Create Realm**
4. Enter a realm name (e.g., `squirrel-wiki`)
5. Click **Create**

Alternatively, you can use the default `master` realm or any existing realm.

## Step 2: Create a Client

1. In the Keycloak Admin Console, select your realm
2. Navigate to **Clients** in the left sidebar
3. Click **Create client**
4. Configure the client:
   - **Client type**: `OpenID Connect`
   - **Client ID**: `squirrel-wiki` (or your preferred client ID)
   - Click **Next**

5. Configure capability settings:
   - **Client authentication**: `ON` (this enables confidential client mode)
   - **Authorization**: `OFF` (not needed for basic authentication)
   - **Authentication flow**: Enable the following:
     - ✅ Standard flow
     - ✅ Direct access grants (optional, for API access)
   - Click **Next**

6. Configure login settings:
   - **Root URL**: `https://your-wiki-domain.com` (replace with your actual domain)
   - **Home URL**: `https://your-wiki-domain.com`
   - **Valid redirect URIs**: 
     - `https://your-wiki-domain.com/signin-oidc`
     - `https://your-wiki-domain.com/*` (for development)
   - **Valid post logout redirect URIs**: 
     - `https://your-wiki-domain.com`
     - `https://your-wiki-domain.com/*`
   - **Web origins**: `https://your-wiki-domain.com` (or `*` for development)
   - Click **Save**

## Step 3: Get Client Credentials

1. In your client configuration, navigate to the **Credentials** tab
2. Copy the **Client secret** - you'll need this for Squirrel Wiki configuration

## Step 4: Configure Client Scopes

1. In your client configuration, navigate to the **Client scopes** tab
2. Ensure the following scopes are assigned (these should be assigned by default):
   - `profile` (required)
   - `email` (required)
   - `roles` (optional, for role mapping)
   - `basic` (optional, but recommended)

**Note**: The `openid` scope is automatically included in OpenID Connect flows and doesn't need to be explicitly added as a client scope. If you see a `basic` scope available, you can add it, but it's not required for basic authentication to work.

## Step 5: Create Groups (Optional but Recommended)

To use group-based authorization in Squirrel Wiki:

1. Navigate to **Groups** in the left sidebar
2. Click **Create group**
3. Create the following groups:
   - `squirrel-admins` (for admin users)
   - `squirrel-editors` (for editor users)
4. Click **Create** for each group

## Step 6: Configure Group Mapper

To include group information in the ID token:

1. Navigate to **Client scopes** in the left sidebar
2. Click on the `profile` scope (or create a custom scope)
3. Navigate to the **Mappers** tab
4. Click **Add mapper** → **By configuration**
5. Select **Group Membership**
6. Configure the mapper:
   - **Name**: `groups`
   - **Token Claim Name**: `groups`
   - **Full group path**: `OFF` (recommended)
   - **Add to ID token**: `ON`
   - **Add to access token**: `ON`
   - **Add to userinfo**: `ON`
7. Click **Save**

## Step 7: Create Users and Assign Groups

1. Navigate to **Users** in the left sidebar
2. Click **Add user**
3. Fill in user details:
   - **Username**: User's username
   - **Email**: User's email address
   - **First name**: User's first name
   - **Last name**: User's last name
4. Click **Create**
5. Navigate to the **Groups** tab for the user
6. Click **Join Group**
7. Select appropriate groups (`squirrel-admins` or `squirrel-editors`)
8. Set a password in the **Credentials** tab

## Step 8: Configure Squirrel Wiki OIDC Plugin

Configure the following settings in Squirrel Wiki (via environment variables or admin UI):

### Required Settings

```bash
PLUGIN_SQUIRREL_AUTH_OIDC_AUTHORITY=https://your-keycloak-domain.com/realms/your-realm-name
PLUGIN_SQUIRREL_AUTH_OIDC_CLIENTID=squirrel-wiki
PLUGIN_SQUIRREL_AUTH_OIDC_CLIENTSECRET=your-client-secret-from-step-3
```

**Important**: The Authority URL must:
- Start with `http://` or `https://` (the protocol is required)
- NOT end with a trailing slash
- Point to your Keycloak realm (e.g., `https://keycloak.example.com/realms/squirrel-wiki`)

**Examples of valid Authority URLs**:
- `https://keycloak.example.com/realms/squirrel-wiki`
- `https://auth.example.com/realms/master`
- `http://localhost:8080/realms/squirrel-wiki` (development only)

**Examples of invalid Authority URLs**:
- `keycloak.example.com/realms/squirrel-wiki` (missing protocol)
- `https://keycloak.example.com/realms/squirrel-wiki/` (trailing slash)

### Optional Settings (with defaults)

```bash
# OAuth scopes to request
PLUGIN_SQUIRREL_AUTH_OIDC_SCOPE="openid profile email"

# Claim mappings
PLUGIN_SQUIRREL_AUTH_OIDC_USERNAMECLAIM=preferred_username
PLUGIN_SQUIRREL_AUTH_OIDC_EMAILCLAIM=email
PLUGIN_SQUIRREL_AUTH_OIDC_DISPLAYNAMECLAIM=name
PLUGIN_SQUIRREL_AUTH_OIDC_GROUPSCLAIM=groups

# Group-based authorization
PLUGIN_SQUIRREL_AUTH_OIDC_ADMINGROUP=squirrel-admins
PLUGIN_SQUIRREL_AUTH_OIDC_EDITORGROUP=squirrel-editors

# User management
PLUGIN_SQUIRREL_AUTH_OIDC_AUTOCREATEUSERS=true

# Security (set to false only for development with HTTP)
PLUGIN_SQUIRREL_AUTH_OIDC_REQUIREHTTPSMETADATA=true
```

## Step 9: Test the Configuration

1. Restart Squirrel Wiki to apply the configuration
2. Navigate to the Squirrel Wiki login page
3. Click **Sign in with OpenID Connect**
4. You should be redirected to Keycloak
5. Log in with a test user
6. You should be redirected back to Squirrel Wiki and logged in

## Troubleshooting

### Common Issues

#### 1. Redirect URI Mismatch
**Error**: "Invalid redirect URI"

**Solution**: Ensure the redirect URI in Keycloak matches exactly:
- Check the **Valid redirect URIs** in your Keycloak client configuration
- The URI should be: `https://your-wiki-domain.com/signin-oidc`

#### 2. Invalid Client Credentials
**Error**: "Invalid client or Invalid client credentials"

**Solution**: 
- Verify the `PLUGIN_SQUIRREL_AUTH_OIDC_CLIENTID` matches the Client ID in Keycloak
- Verify the `PLUGIN_SQUIRREL_AUTH_OIDC_CLIENTSECRET` matches the client secret in Keycloak
- Ensure **Client authentication** is enabled in Keycloak

#### 3. Groups Not Working
**Error**: Users don't have correct permissions

**Solution**:
- Verify the group mapper is configured correctly (Step 6)
- Check that users are assigned to the correct groups
- Verify `PLUGIN_SQUIRREL_AUTH_OIDC_GROUPSCLAIM=groups` matches the Token Claim Name in the mapper
- Ensure group names match: `PLUGIN_SQUIRREL_AUTH_OIDC_ADMINGROUP` and `PLUGIN_SQUIRREL_AUTH_OIDC_EDITORGROUP`

#### 4. HTTPS Metadata Error
**Error**: "The MetadataAddress or Authority must use HTTPS"

**Solution** (for development only):
```bash
PLUGIN_SQUIRREL_AUTH_OIDC_REQUIREHTTPSMETADATA=false
```

**Note**: Never disable HTTPS metadata validation in production!

#### 5. User Not Found
**Error**: "User account not found"

**Solution**:
- Ensure `PLUGIN_SQUIRREL_AUTH_OIDC_AUTOCREATEUSERS=true` if you want automatic user creation
- Or manually create users in Squirrel Wiki before they log in

### Debugging Tips

1. **Check Keycloak Logs**: Look for authentication errors in Keycloak server logs
2. **Check Squirrel Wiki Logs**: Review application logs for OIDC-related errors
3. **Verify Token Claims**: Use Keycloak's token introspection to verify claims are included
4. **Test with Browser DevTools**: Monitor network requests during login to identify issues

## Advanced Configuration

### Custom Claim Mappings

If your Keycloak uses different claim names, you can customize the mappings:

```bash
# Example: Using 'sub' claim for username
PLUGIN_SQUIRREL_AUTH_OIDC_USERNAMECLAIM=sub

# Example: Using 'full_name' claim for display name
PLUGIN_SQUIRREL_AUTH_OIDC_DISPLAYNAMECLAIM=full_name

# Example: Using 'realm_roles' for groups
PLUGIN_SQUIRREL_AUTH_OIDC_GROUPSCLAIM=realm_roles
```

### Role-Based Groups

Instead of using groups, you can use Keycloak roles:

1. Create roles in Keycloak (e.g., `admin`, `editor`)
2. Create a role mapper similar to the group mapper
3. Configure Squirrel Wiki to use the role claim:
```bash
PLUGIN_SQUIRREL_AUTH_OIDC_GROUPSCLAIM=realm_roles
PLUGIN_SQUIRREL_AUTH_OIDC_ADMINGROUP=admin
PLUGIN_SQUIRREL_AUTH_OIDC_EDITORGROUP=editor
```

### Multiple Realms

If you need to support multiple Keycloak realms, you'll need to:
1. Create separate OIDC plugin instances (requires code modification)
2. Or use a single realm with different clients for different environments

## Security Best Practices

1. **Always use HTTPS** in production
2. **Keep client secrets secure** - never commit them to version control
3. **Use strong passwords** for Keycloak admin accounts
4. **Regularly rotate client secrets**
5. **Limit redirect URIs** to only necessary URLs
6. **Enable MFA** in Keycloak for admin users
7. **Review user permissions** regularly
8. **Monitor authentication logs** for suspicious activity

## Additional Resources

- [Keycloak Documentation](https://www.keycloak.org/documentation)
- [OpenID Connect Specification](https://openid.net/connect/)
- [Squirrel Wiki Documentation](../README.md)

## Support

If you encounter issues not covered in this guide:
1. Check the Squirrel Wiki logs for detailed error messages
2. Review Keycloak server logs
3. Consult the Keycloak community forums
4. Open an issue on the Squirrel Wiki GitHub repository
