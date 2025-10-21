<a id="mysql-server-setup-linux"></a>
## 🖥️ MySQL Server Setup Ubuntu

Image Perfect requires a local MySQL 8.0+ server.

---

📋 Temporary Instructions

Full step-by-step setup directions for Ubuntu are coming soon.
For now, here’s the basic outline:

1. **Install MySQL**:
```
sudo apt update
sudo apt install mysql-server

```
2. **Set up users**
	- Configure your root password.
	- Create a dedicated ImagePerfect user with full access to the imageperfect database.
3. Run the [database setup commands](CREATE_DATABASE_COMMANDS.md).

📚 **Helpful Resource**:


I recommend following this guide:
[How To Install MySQL on Ubuntu 22.04 (DigitalOcean)](https://www.digitalocean.com/community/tutorials/how-to-install-mysql-on-ubuntu-22-04)

> This guide is written for Ubuntu Server, but the steps are nearly identical on a regular desktop install.